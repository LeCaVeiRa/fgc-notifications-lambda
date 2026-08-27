using Amazon.Lambda.APIGatewayEvents;
using Fgc.Notifications.Lambda.Application.Interfaces;
using Fgc.Notifications.Lambda.Domain.Entities;
using Fgc.Notifications.Lambda.HistoryApi;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Fgc.Notifications.Lambda.Tests;

public class HistoryApiFunctionTests
{
    private const string JwtKey = "Test-Only-Symmetric-Key-For-Fgc-Notifications-Lambda-Tests";
    private const string JwtIssuer = "Fgc.UsersAPI";
    private const string JwtAudience = "Fgc.CatalogAPI";

    private readonly Mock<INotificationRepository> _repositoryMock;
    private readonly Function _function;

    public HistoryApiFunctionTests()
    {
        _repositoryMock = new Mock<INotificationRepository>();
        _function = new Function(_repositoryMock.Object, JwtKey, JwtIssuer, JwtAudience);
    }

    [Fact]
    public async Task FunctionHandler_NoToken_Returns401()
    {
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Headers = new Dictionary<string, string>()
        };

        var response = await _function.FunctionHandler(request, MockContext());

        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task FunctionHandler_ValidTokenNonAdmin_Returns403()
    {
        var token = GenerateToken(role: "User");
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" }
        };

        var response = await _function.FunctionHandler(request, MockContext());

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task FunctionHandler_ValidTokenAdmin_Returns200WithNotifications()
    {
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Notification.CreateWelcome("Ana", "ana@teste.com")]);

        var token = GenerateToken(role: "Admin");
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" }
        };

        var response = await _function.FunctionHandler(request, MockContext());

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("ana@teste.com", response.Body);
    }

    private static string GenerateToken(string role)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static Amazon.Lambda.Core.ILambdaContext MockContext()
    {
        var loggerMock = new Mock<Amazon.Lambda.Core.ILambdaLogger>();
        var contextMock = new Mock<Amazon.Lambda.Core.ILambdaContext>();
        contextMock.SetupGet(c => c.Logger).Returns(loggerMock.Object);
        return contextMock.Object;
    }
}
