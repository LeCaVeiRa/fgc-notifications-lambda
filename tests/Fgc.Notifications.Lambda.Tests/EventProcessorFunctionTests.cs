using Amazon.Lambda.Core;
using Amazon.Lambda.MQEvents;
using Fgc.Notifications.Lambda.Application.Interfaces;
using Fgc.Notifications.Lambda.EventProcessor;
using Moq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Fgc.Notifications.Lambda.Tests;

public class EventProcessorFunctionTests
{
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Function _function;
    private readonly ILambdaContext _context;

    public EventProcessorFunctionTests()
    {
        _notificationServiceMock = new Mock<INotificationService>();
        _function = new Function(_notificationServiceMock.Object);

        var loggerMock = new Mock<ILambdaLogger>();
        var contextMock = new Mock<ILambdaContext>();
        contextMock.SetupGet(c => c.Logger).Returns(loggerMock.Object);
        _context = contextMock.Object;
    }

    [Fact]
    public async Task FunctionHandler_UserCreatedAndRejectedPayment_OnlySendsWelcomeEmail()
    {
        var rabbitMqEvent = new RabbitMQEvent
        {
            RmqMessagesByQueue = new Dictionary<string, List<RabbitMQEvent.RabbitMQMessage>>
            {
                ["notifications-lambda::/"] =
                [
                    BuildMessage("UserCreatedEvent", new
                    {
                        Id = Guid.NewGuid(),
                        Name = "Ana Teste",
                        Email = "ana@teste.com",
                        CreatedAt = DateTime.UtcNow
                    }),
                    BuildMessage("PaymentProcessedEvent", new
                    {
                        OrderedId = Guid.NewGuid(),
                        UserId = Guid.NewGuid(),
                        GameId = Guid.NewGuid(),
                        Price = 59.90m,
                        Status = "Rejected",
                        ProcessedAt = DateTime.UtcNow
                    })
                ]
            }
        };

        await _function.FunctionHandler(rabbitMqEvent, _context);

        _notificationServiceMock.Verify(
            s => s.SendWelcomeEmailAsync("Ana Teste", "ana@teste.com", It.IsAny<CancellationToken>()),
            Times.Once);

        _notificationServiceMock.Verify(
            s => s.SendPurchaseConfirmationEmailAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_ApprovedPayment_SendsPurchaseConfirmation()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var rabbitMqEvent = new RabbitMQEvent
        {
            RmqMessagesByQueue = new Dictionary<string, List<RabbitMQEvent.RabbitMQMessage>>
            {
                ["notifications-lambda::/"] =
                [
                    BuildMessage("PaymentProcessedEvent", new
                    {
                        OrderedId = Guid.NewGuid(),
                        UserId = userId,
                        GameId = gameId,
                        Price = 59.90m,
                        Status = "Approved",
                        ProcessedAt = DateTime.UtcNow
                    })
                ]
            }
        };

        await _function.FunctionHandler(rabbitMqEvent, _context);

        _notificationServiceMock.Verify(
            s => s.SendPurchaseConfirmationEmailAsync(userId, gameId, 59.90m, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static RabbitMQEvent.RabbitMQMessage BuildMessage(string eventTypeName, object payload)
    {
        var envelope = new
        {
            messageType = new[] { $"urn:message:Fgc.MessageContracts.Events:{eventTypeName}" },
            message = payload
        };

        var json = JsonSerializer.Serialize(envelope);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        return new RabbitMQEvent.RabbitMQMessage
        {
            Data = base64,
            Redelivered = false
        };
    }
}
