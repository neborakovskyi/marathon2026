using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.Waiters;
using APIHandler.Models.WaiterList;
using APIHandler.Services.Waiters;
using Moq;
using System.Collections.Generic;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Waiters;

public class GetWaitersByLocationIdHandlerTests
{
    private readonly Mock<IWaiterService> _waiterService = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public GetWaitersByLocationIdHandlerTests()
    {
        _context.Setup(context => context.Logger).Returns(_logger.Object);
    }

    [Fact]
    public async Task HandleRequest_ValidLocationId_ReturnsWaiterIdsAndLocationIds()
    {
        const string locationId = "location1";
        _waiterService
            .Setup(service => service.GetWaitersByLocationIdAsync(locationId))
            .ReturnsAsync(
            [
                new WaiterListItem { UserId = "user1", LocationId = locationId },
                new WaiterListItem { UserId = "user2", LocationId = locationId }
            ]);

        var handler = new GetWaitersByLocationIdHandler(_waiterService.Object);
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { ["locationId"] = locationId }
        };

        var result = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(200, result.StatusCode);
        using var response = JsonDocument.Parse(result.Body);
        Assert.Equal(JsonValueKind.Array, response.RootElement.ValueKind);
        Assert.Equal(2, response.RootElement.GetArrayLength());
        foreach (var waiter in response.RootElement.EnumerateArray())
        {
            Assert.Equal(2, waiter.EnumerateObject().Count());
            Assert.True(waiter.TryGetProperty("userId", out _));
            Assert.Equal(locationId, waiter.GetProperty("locationId").GetString());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task HandleRequest_MissingOrBlankLocationId_ReturnsBadRequest(string? locationId)
    {
        var handler = new GetWaitersByLocationIdHandler(_waiterService.Object);
        var request = new APIGatewayProxyRequest
        {
            PathParameters = locationId is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["locationId"] = locationId }
        };

        var result = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(400, result.StatusCode);
        _waiterService.Verify(
            service => service.GetWaitersByLocationIdAsync(It.IsAny<string>()),
            Times.Never);
    }
}
