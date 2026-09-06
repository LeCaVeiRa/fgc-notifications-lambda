using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.MQEvents;
using Fgc.Notifications.Lambda.Application.Events;
using Fgc.Notifications.Lambda.Application.Interfaces;
using Fgc.Notifications.Lambda.Application.Messaging;
using Fgc.Notifications.Lambda.Application.Services;
using Fgc.Notifications.Lambda.Domain.Exceptions;
using Fgc.Notifications.Lambda.Infrastructure.Email;
using Fgc.Notifications.Lambda.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Fgc.Notifications.Lambda.EventProcessor;

public class Function
{
    private const string ApprovedStatus = "Approved";

    private readonly INotificationService _notificationService;

    public Function() : this(BuildDefaultNotificationService())
    {
    }

    internal Function(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private static INotificationService BuildDefaultNotificationService()
    {
        // Sem prefixo "AWS_": o Lambda (e o SAM CLI local) trata variáveis com esse prefixo como
        // reservadas e as descarta silenciosamente se o usuário tentar defini-las.
        var serviceUrl = Environment.GetEnvironmentVariable("DYNAMODB_LOCAL_ENDPOINT");
        var config = new AmazonDynamoDBConfig();

        AmazonDynamoDBClient dynamoDb;
        if (!string.IsNullOrEmpty(serviceUrl))
        {
            config.ServiceURL = serviceUrl;
            // `sam local invoke` injeta suas próprias credenciais/sessão (do profile AWS do host,
            // possivelmente inválidas) que o DynamoDB Local rejeita com "security token invalid" -
            // credenciais fixas ignoram esse comportamento e valem só para o endpoint local.
            dynamoDb = new AmazonDynamoDBClient(new Amazon.Runtime.BasicAWSCredentials("local", "local"), config);
        }
        else
        {
            dynamoDb = new AmazonDynamoDBClient(config);
        }
        var repository = new DynamoDbNotificationRepository(dynamoDb);
        // O runtime Lambda captura stdout/stderr automaticamente para o CloudWatch Logs, e
        // `sam local invoke` exibe no terminal - por isso um logger de console real (não
        // NullLogger) já é suficiente para os logs de paridade aparecerem nos dois casos.
        var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());
        var emailSender = new LoggingEmailSender();
        return new NotificationService(repository, emailSender, loggerFactory.CreateLogger<NotificationService>());
    }

    public async Task FunctionHandler(RabbitMQEvent rabbitMqEvent, ILambdaContext context)
    {
        foreach (var (queueName, messages) in rabbitMqEvent.RmqMessagesByQueue)
        {
            foreach (var message in messages)
            {
                await ProcessMessageAsync(queueName, message, context);
            }
        }
    }

    private async Task ProcessMessageAsync(string queueName, RabbitMQEvent.RabbitMQMessage message, ILambdaContext context)
    {
        string json;
        try
        {
            var raw = Convert.FromBase64String(message.Data);
            json = Encoding.UTF8.GetString(raw);
        }
        catch (FormatException ex)
        {
            context.Logger.LogWarning($"[{queueName}] Não foi possível decodificar o payload base64: {ex.Message}");
            return;
        }

        var envelope = MassTransitEnvelope.Parse(json);
        var eventTypeName = envelope?.ResolveEventTypeName();

        if (envelope is null || eventTypeName is null)
        {
            context.Logger.LogWarning($"[{queueName}] Mensagem sem envelope MassTransit reconhecível. Ignorando.");
            return;
        }

        try
        {
            switch (eventTypeName)
            {
                case nameof(UserCreatedEvent):
                    await HandleUserCreatedAsync(envelope.Message);
                    break;

                case nameof(PaymentProcessedEvent):
                    await HandlePaymentProcessedAsync(envelope.Message, context);
                    break;

                default:
                    context.Logger.LogInformation($"[{queueName}] Tipo de evento desconhecido '{eventTypeName}'. Ignorando.");
                    break;
            }
        }
        catch (NotificationDomainException ex)
        {
            // Dado inválido no evento: não é recuperável via retry, loga e segue para a próxima mensagem do batch.
            context.Logger.LogWarning($"[{queueName}] Evento '{eventTypeName}' inválido: {ex.Message}");
        }
        // Exceções de infraestrutura (ex.: DynamoDB indisponível) propagam de propósito, para o
        // Lambda reprocessar via retry nativo do Event Source Mapping.
    }

    private Task HandleUserCreatedAsync(JsonElement message)
    {
        var userCreated = message.Deserialize<UserCreatedEvent>(JsonOptions)
            ?? throw new NotificationDomainException("UserCreatedEvent payload vazio.");

        return _notificationService.SendWelcomeEmailAsync(userCreated.Name, userCreated.Email);
    }

    private Task HandlePaymentProcessedAsync(JsonElement message, ILambdaContext context)
    {
        var paymentProcessed = message.Deserialize<PaymentProcessedEvent>(JsonOptions)
            ?? throw new NotificationDomainException("PaymentProcessedEvent payload vazio.");

        if (paymentProcessed.Status != ApprovedStatus)
        {
            context.Logger.LogWarning($"⚠️ [PAYMENT REJECTED] Payment {paymentProcessed.OrderedId} foi rejeitado.");
            return Task.CompletedTask;
        }

        return _notificationService.SendPurchaseConfirmationEmailAsync(paymentProcessed.UserId, paymentProcessed.GameId, paymentProcessed.Price);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new FlexibleDecimalJsonConverter() }
    };
}
