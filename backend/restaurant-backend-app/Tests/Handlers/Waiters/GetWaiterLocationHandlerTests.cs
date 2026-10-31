using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.Waiters;
using APIHandler.Services.Waiters;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Waiters;

public class GetWaiterLocationHandlerTests
{
    private readonly Mock<IWaiterService> _waiterService = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public GetWaiterLocationHandlerTests()
    {
        _context.SetupGet(context => context.Logger).Returns(_logger.Object);
    }

    [Fact]
    public async Task HandleRequest_ValidWaiterWithLocation_ReturnsLocationId()
    {
        _waiterService.Setup(service => service.GetWaiterLocationIdAsync("waiter-1"))
            .ReturnsAsync("location-1");
        var handler = new GetWaiterLocationHandler(_waiterService.Object);
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { ["userId"] = "waiter-1" }
        };

        var response = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal("location-1", body.RootElement.GetProperty("locationId").GetString());
        _waiterService.Verify(service => service.GetWaiterLocationIdAsync("waiter-1"), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_WaiterHasNoLocation_ReturnsNotFound()
    {
        _waiterService.Setup(service => service.GetWaiterLocationIdAsync("waiter-2"))
            .ReturnsAsync((string?)null);
        var handler = new GetWaiterLocationHandler(_waiterService.Object);
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { ["userId"] = "waiter-2" }
        };

        var response = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("waiter-2", response.Body);
        _waiterService.Verify(service => service.GetWaiterLocationIdAsync("waiter-2"), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleRequest_MissingWaiterId_ReturnsBadRequestWithoutServiceCall(string? waiterId)
    {
        var handler = new GetWaiterLocationHandler(_waiterService.Object);
        var request = new APIGatewayProxyRequest
        {
            PathParameters = waiterId is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["userId"] = waiterId }
        };

        var response = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(400, response.StatusCode);
        _waiterService.Verify(service => service.GetWaiterLocationIdAsync(It.IsAny<string>()), Times.Never);
    }
}
