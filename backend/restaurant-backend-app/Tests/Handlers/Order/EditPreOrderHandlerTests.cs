using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Handlers.Order;
using APIHandler.Services.Orders;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Order;

public class EditPreOrderHandlerTests
{
    private readonly Mock<IOrderService> _orders = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public EditPreOrderHandlerTests() => _context.SetupGet(c => c.Logger).Returns(_logger.Object);

    [Fact]
    public async Task HandleRequest_CustomerEditsPreOrder_UpdatesOrder()
    {
        var item = new OrderItem { DishId = "dish-1", Quantity = 2 };
        _orders.Setup(service => service.EditPreOrderAsync("customer-1", "reservation-1",
                It.Is<List<OrderItem>>(items => items.Single().DishId == item.DishId && items.Single().Quantity == 2),
                _context.Object))
            .Returns(Task.CompletedTask);

        var response = await Handle(Request("Customer", "customer-1", "reservation-1", Body(item)));

        Assert.Equal(200, response.StatusCode);
        _orders.Verify(service => service.EditPreOrderAsync("customer-1", "reservation-1",
            It.IsAny<List<OrderItem>>(), _context.Object), Times.Once);
        _orders.Verify(service => service.EditPreOrderForWaiterAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<OrderItem>>(), It.IsAny<ILambdaContext>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_WaiterEditsPreOrder_UpdatesOrderAsWaiter()
    {
        _orders.Setup(service => service.EditPreOrderForWaiterAsync("waiter-1", "reservation-1",
                It.IsAny<List<OrderItem>>(), _context.Object))
            .Returns(Task.CompletedTask);

        var response = await Handle(Request("Waiter", "waiter-1", "reservation-1",
            Body(new OrderItem { DishId = "dish-1", Quantity = 1 })));

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Order updated successfully", response.Body);
        _orders.Verify(service => service.EditPreOrderForWaiterAsync("waiter-1", "reservation-1",
            It.IsAny<List<OrderItem>>(), _context.Object), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_MissingRole_ReturnsUnauthorized()
    {
        var response = await Handle(new APIGatewayProxyRequest
        {
            Body = Body(new OrderItem { DishId = "dish-1", Quantity = 1 }),
            PathParameters = new Dictionary<string, string> { ["reservationId"] = "reservation-1" },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext { Claims = new Dictionary<string, string>() }
            }
        });

        Assert.Equal(401, response.StatusCode);
        _orders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_MissingUserId_ReturnsBadRequest()
    {
        var response = await Handle(Request("Customer", null, "reservation-1",
            Body(new OrderItem { DishId = "dish-1", Quantity = 1 })));

        Assert.Equal(400, response.StatusCode);
        _orders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_MissingReservationId_ReturnsBadRequest()
    {
        var response = await Handle(Request("Customer", "customer-1", null,
            Body(new OrderItem { DishId = "dish-1", Quantity = 1 })));

        Assert.Equal(400, response.StatusCode);
        _orders.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"orderItems\":[]}")]
    public async Task HandleRequest_InvalidOrEmptyOrderItems_ReturnsBadRequest(string body)
    {
        var response = await Handle(Request("Customer", "customer-1", "reservation-1", body));

        Assert.Equal(400, response.StatusCode);
        _orders.VerifyNoOtherCalls();
    }

    private Task<APIGatewayProxyResponse> Handle(APIGatewayProxyRequest request) =>
        new EditPreOrderHandler(_orders.Object).HandleRequest(request, _context.Object);

    private APIGatewayProxyRequest Request(string role, string? userId, string? reservationId, string body) =>
        new()
        {
            Body = body,
            PathParameters = reservationId is null ? [] : new Dictionary<string, string> { ["reservationId"] = reservationId },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>
                    {
                        ["cognito:groups"] = role,
                        ["sub"] = userId ?? string.Empty
                    }
                }
            }
        };

    private static string Body(OrderItem item) => JsonSerializer.Serialize(new { orderItems = new[] { item } });
}
