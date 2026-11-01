using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Handlers.Order;
using APIHandler.Models.Orders;
using APIHandler.Services.Dishes;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using APIHandler.Services.Reservations;
using Moq;
using System.Text.Json;
using OrderEntity = APIHandler.Entities.Order;

namespace APIHandler.Tests.Handlers.Order;

public class CreateOrderHandlerTests
{
    private readonly Mock<IDishService> _dishes = new();
    private readonly Mock<IReservationService> _reservations = new();
    private readonly Mock<ILocationService> _locations = new();
    private readonly Mock<IOrderService> _orders = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public CreateOrderHandlerTests() => _context.SetupGet(c => c.Logger).Returns(_logger.Object);

    [Fact]
    public async Task HandleRequest_CustomerWithReservedReservation_CreatesPreOrder()
    {
        var reservation = Reservation(ReservationStatus.Reserved);
        SetupRequestDependencies(reservation, Location());
        var dish = Dish("dish-1", DishState.Available);
        _dishes.Setup(s => s.GetDishByIdAsync(dish.Id)).ReturnsAsync(dish);
        OrderEntity? created = null;
        _orders.Setup(s => s.AddOrderAsync(It.IsAny<OrderEntity>()))
            .Callback<OrderEntity>(order => created = order).Returns(Task.CompletedTask);

        var response = await Handle(Request(Body([new OrderItem { DishId = dish.Id, Quantity = 2 }]), "customer-1", "Customer"));

        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("customer-1", created.CustomerId);
        Assert.Equal(reservation.Id, created.ReservationId);
        Assert.Equal("Central", created.ReservationAddress);
        Assert.Equal(2, created.Dishes.Single().Quantity);
        Assert.Contains("dish-1", response.Body);
        _orders.Verify(s => s.AddOrderAsync(created), Times.Once);
        _orders.Verify(s => s.AddOrderForWaiterAsync(It.IsAny<OrderEntity>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_WaiterWithInProgressReservation_CreatesOrderForReservationCustomer()
    {
        var reservation = Reservation(ReservationStatus.InProgress);
        reservation.WaiterId = "waiter-1";
        SetupRequestDependencies(reservation, Location());
        var dish = Dish("dish-1", DishState.Available);
        _dishes.Setup(s => s.GetDishByIdAsync(dish.Id)).ReturnsAsync(dish);
        OrderEntity? created = null;
        _orders.Setup(s => s.AddOrderForWaiterAsync(It.IsAny<OrderEntity>()))
            .Callback<OrderEntity>(order => created = order).Returns(Task.CompletedTask);

        var response = await Handle(Request(Body([new OrderItem { DishId = dish.Id, Quantity = 1 }]), "waiter-1", "Waiter"));

        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("customer-1", created.CustomerId);
        _orders.Verify(s => s.AddOrderForWaiterAsync(created), Times.Once);
        _orders.Verify(s => s.AddOrderAsync(It.IsAny<OrderEntity>()), Times.Never);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task HandleRequest_InvalidBody_ReturnsBadRequest(string body)
    {
        var response = await Handle(Request(body, "customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
        _orders.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("dish-1", 0)]
    [InlineData("dish-1", -1)]
    public async Task HandleRequest_InvalidOrderItem_ReturnsBadRequest(string dishId, int quantity)
    {
        var response = await Handle(Request(Body([new OrderItem { DishId = dishId, Quantity = quantity }]), "customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        _dishes.Verify(s => s.GetDishByIdAsync(It.IsAny<string>()), Times.Never);
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_EmptyOrderItems_ReturnsBadRequest()
    {
        var response = await Handle(Request(Body([]), "customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_DishMissingOrUnavailable_ReturnsBadRequest()
    {
        _dishes.Setup(s => s.GetDishByIdAsync("dish-1")).ReturnsAsync((Dish)null!);

        var response = await Handle(Request(Body([new OrderItem { DishId = "dish-1", Quantity = 1 }]), "customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
        _orders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_ReservationMissing_ReturnsBadRequest()
    {
        _dishes.Setup(s => s.GetDishByIdAsync("dish-1")).ReturnsAsync(Dish("dish-1", DishState.Available));
        _reservations.Setup(s => s.GetReservationByIdAsync("reservation-1")).ReturnsAsync((Reservation?)null);

        var response = await Handle(Request(Body([new OrderItem { DishId = "dish-1", Quantity = 1 }]), "customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Reservation not found", response.Body);
    }

    [Fact]
    public async Task HandleRequest_LocationMissing_ReturnsBadRequest()
    {
        var reservation = Reservation(ReservationStatus.Reserved);
        SetupRequestDependencies(reservation, null);
        _dishes.Setup(s => s.GetDishByIdAsync("dish-1")).ReturnsAsync(Dish("dish-1", DishState.Available));

        var response = await Handle(Request(Body([new OrderItem { DishId = "dish-1", Quantity = 1 }]), "customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Location not found", response.Body);
    }

    [Fact]
    public async Task HandleRequest_CallerDoesNotOwnReservation_ReturnsForbidden()
    {
        var reservation = Reservation(ReservationStatus.Reserved);
        SetupRequestDependencies(reservation, Location());
        _dishes.Setup(s => s.GetDishByIdAsync("dish-1")).ReturnsAsync(Dish("dish-1", DishState.Available));

        var response = await Handle(Request(Body([new OrderItem { DishId = "dish-1", Quantity = 1 }]), "other-customer", "Customer"));

        Assert.Equal(403, response.StatusCode);
        _orders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_CustomerReservationNotReserved_ReturnsBadRequest()
    {
        var reservation = Reservation(ReservationStatus.Completed);
        SetupRequestDependencies(reservation, Location());
        _dishes.Setup(s => s.GetDishByIdAsync("dish-1")).ReturnsAsync(Dish("dish-1", DishState.Available));

        var response = await Handle(Request(Body([new OrderItem { DishId = "dish-1", Quantity = 1 }]), "customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Pre-order is allowed", response.Body);
    }

    [Fact]
    public async Task HandleRequest_WaiterNotAssignedToReservation_ReturnsForbidden()
    {
        var reservation = Reservation(ReservationStatus.InProgress);
        SetupRequestDependencies(reservation, Location());
        _dishes.Setup(s => s.GetDishByIdAsync("dish-1")).ReturnsAsync(Dish("dish-1", DishState.Available));

        var response = await Handle(Request(Body([new OrderItem { DishId = "dish-1", Quantity = 1 }]), "different-waiter", "Waiter"));

        Assert.Equal(403, response.StatusCode);
        _orders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_WaiterReservationNotInProgress_ReturnsBadRequest()
    {
        var reservation = Reservation(ReservationStatus.Reserved);
        SetupRequestDependencies(reservation, Location());
        _dishes.Setup(s => s.GetDishByIdAsync("dish-1")).ReturnsAsync(Dish("dish-1", DishState.Available));

        var response = await Handle(Request(Body([new OrderItem { DishId = "dish-1", Quantity = 1 }]), "waiter-1", "Waiter"));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("InProgress", response.Body);
    }

    [Fact]
    public async Task HandleRequest_DishServiceThrows_MapsUnexpectedErrorToServerError()
    {
        _dishes.Setup(s => s.GetDishByIdAsync("dish-1")).ThrowsAsync(new InvalidOperationException("dish lookup failed"));

        var response = await Handle(Request(Body([new OrderItem { DishId = "dish-1", Quantity = 1 }]), "customer-1", "Customer"));

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("dish lookup failed", response.Body);
    }

    private void SetupRequestDependencies(Reservation reservation, APIHandler.Entities.Location? location)
    {
        _reservations.Setup(s => s.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _locations.Setup(s => s.GetLocationByTableIdAsync(reservation.TableId)).ReturnsAsync(location!);
    }

    private Task<APIGatewayProxyResponse> Handle(APIGatewayProxyRequest request) =>
        new CreateOrderHandler(_dishes.Object, _reservations.Object, _locations.Object, _orders.Object)
            .HandleRequest(request, _context.Object);

    private static APIGatewayProxyRequest Request(string body, string userId, string role) => new()
    {
        Body = body,
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = new Dictionary<string, string>
                {
                    ["sub"] = userId,
                    ["cognito:groups"] = role
                }
            }
        }
    };

    private static string Body(List<OrderItem> items) => JsonSerializer.Serialize(new CreateOrderRequest
    {
        ReservationId = "reservation-1",
        OrderItems = items
    });

    private static Reservation Reservation(ReservationStatus status) => new()
    {
        Id = "reservation-1", TableId = "table-1", CustomerId = "customer-1", WaiterId = "waiter-1",
        Status = status, StartTime = new DateTime(2026, 8, 12, 17, 0, 0), EndTime = new DateTime(2026, 8, 12, 18, 0, 0)
    };

    private static APIHandler.Entities.Location Location() => new()
    {
        Id = "location-1", Address = "Central", Description = "Restaurant", AverageOccupancy = "medium"
    };

    private static Dish Dish(string id, DishState state) => new()
    {
        Id = id, Name = "Soup", Description = "Vegetable soup", Price = 10, State = state,
        Weight = "300g", Calories = "200", Proteins = "8g", Fats = "4g",
        Carbohydrates = "25g", Vitamins = "A", LocationId = "location-1"
    };
}
