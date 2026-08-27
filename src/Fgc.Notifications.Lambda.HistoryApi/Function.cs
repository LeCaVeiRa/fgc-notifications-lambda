using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Fgc.Notifications.Lambda.Application.DTOs;
using Fgc.Notifications.Lambda.Application.Interfaces;
using Fgc.Notifications.Lambda.Infrastructure.Persistence;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Fgc.Notifications.Lambda.HistoryApi;

public class Function
{
    private readonly INotificationRepository _repository;
    private readonly TokenValidationParameters _tokenValidationParameters;

    public Function() : this(
        BuildDefaultRepository(),
        Environment.GetEnvironmentVariable("JWT_KEY") ?? throw new InvalidOperationException("JWT_KEY não configurada."),
        Environment.GetEnvironmentVariable("JWT_ISSUER") ?? throw new InvalidOperationException("JWT_ISSUER não configurada."),
        Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? throw new InvalidOperationException("JWT_AUDIENCE não configurada."))
    {
    }

    internal Function(INotificationRepository repository, string jwtKey, string jwtIssuer, string jwtAudience)
    {
        _repository = repository;
        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    }

    private static INotificationRepository BuildDefaultRepository()
    {
        // Sem prefixo "AWS_": ver nota equivalente em EventProcessor/Function.cs.
        var serviceUrl = Environment.GetEnvironmentVariable("DYNAMODB_LOCAL_ENDPOINT");
        var config = new AmazonDynamoDBConfig();

        if (!string.IsNullOrEmpty(serviceUrl))
        {
            config.ServiceURL = serviceUrl;
            // Ver nota equivalente em EventProcessor/Function.cs sobre credenciais injetadas pelo sam local.
            return new DynamoDbNotificationRepository(
                new AmazonDynamoDBClient(new Amazon.Runtime.BasicAWSCredentials("local", "local"), config));
        }

        return new DynamoDbNotificationRepository(new AmazonDynamoDBClient(config));
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var principal = TryAuthenticate(request);

        if (principal is null)
        {
            return Unauthorized();
        }

        if (!principal.IsInRole("Admin"))
        {
            return Forbidden();
        }

        var notifications = await _repository.GetAllAsync();

        var response = notifications
            .Select(n => new NotificationResponse(n.Id, n.Type.ToString(), n.RecipientName, n.RecipientEmail, n.Subject, n.SentAt))
            .ToList();

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(response),
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
        };
    }

    private ClaimsPrincipal? TryAuthenticate(APIGatewayHttpApiV2ProxyRequest request)
    {
        var header = request.Headers?.FirstOrDefault(h => string.Equals(h.Key, "Authorization", StringComparison.OrdinalIgnoreCase)).Value;

        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = header["Bearer ".Length..].Trim();

        try
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(token, _tokenValidationParameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    private static APIGatewayHttpApiV2ProxyResponse Unauthorized() => new()
    {
        StatusCode = 401,
        Body = JsonSerializer.Serialize(new { message = "Unauthorized" }),
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
    };

    private static APIGatewayHttpApiV2ProxyResponse Forbidden() => new()
    {
        StatusCode = 403,
        Body = JsonSerializer.Serialize(new { message = "Forbidden" }),
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
    };
}
