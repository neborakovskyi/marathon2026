using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.Locations;
using APIHandler.Repositories.Notifications;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.Notifications;
using Moq;

namespace APIHandler.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ILocationRepository> _locations = new();
    private readonly Mock<IDishRepository> _dishes = new();
    private readonly NotificationService _service;

    public NotificationServiceTests() =>
        _service = new NotificationService(_notifications.Object, _reservations.Object, _orders.Object,
            _locations.Object, _dishes.Object);

    [Fact]
    public async Task CreateOrderNotificationAsync_ValidOrder_SavesNotificationWithSummary()
    {
        var reservation = Reservation("customer-1");
        var order = Order([new OrderItem { DishId = "dish-1", Quantity = 2 }]);
        _reservations.Setup(repository => repository.GetReservationByIdAsync("reservation-1")).ReturnsAsync(reservation);
        _orders.Setup(repository => repository.GetOrderByIdAsync("order-1", "customer-1")).ReturnsAsync(order);
        _locations.Setup(repository => repository.GetTableByIdAsync("table-1"))
            .ReturnsAsync(new Table { Id = "table-1", LocationId = "location-1", TableNumber = 7 });
        _dishes.Setup(repository => repository.GetDishByIdAsync("dish-1")).ReturnsAsync(Dish("dish-1", "Noodles"));
        Notification? saved = null;
        _notifications.Setup(repository => repository.SaveNotificationAsync(It.IsAny<Notification>()))
            .Callback<Notification>(notification => saved = notification)
            .Returns(Task.CompletedTask);

        var result = await _service.CreateOrderNotificationAsync("waiter-1", "order-1", "reservation-1");

        Assert.Same(saved, result);
        Assert.Equal("waiter-1", result.UserId);
        Assert.Equal("order-1", result.OrderId);
        Assert.Equal("reservation-1", result.ReservationId);
        Assert.Contains("Table 7", result.Message);
        Assert.Contains("2x Noodles", result.Message);
        _notifications.Verify(repository => repository.SaveNotificationAsync(result), Times.Once);
    }

    [Fact]
    public async Task CreateOrderNotificationAsync_ReservationMissing_ThrowsNotFound()
    {
        _reservations.Setup(repository => repository.GetReservationByIdAsync("reservation-1"))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _service.CreateOrderNotificationAsync("waiter-1", "order-1", "reservation-1"));

        _orders.VerifyNoOtherCalls();
        _notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateOrderNotificationAsync_ReservationHasNoCustomerId_ThrowsInvalidOperation()
    {
        _reservations.Setup(repository => repository.GetReservationByIdAsync("reservation-1"))
            .ReturnsAsync(Reservation(null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateOrderNotificationAsync("waiter-1", "order-1", "reservation-1"));
    }

    [Fact]
    public async Task CreateOrderNotificationAsync_OrderMissing_ThrowsValidationException()
    {
        _reservations.Setup(repository => repository.GetReservationByIdAsync("reservation-1"))
            .ReturnsAsync(Reservation("customer-1"));
        _orders.Setup(repository => repository.GetOrderByIdAsync("order-1", "customer-1"))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateOrderNotificationAsync("waiter-1", "order-1", "reservation-1"));
    }

    [Fact]
    public async Task CreateOrderNotificationAsync_NoTableOrDishes_UsesFallbackMessageValues()
    {
        _reservations.Setup(repository => repository.GetReservationByIdAsync("reservation-1"))
            .ReturnsAsync(Reservation("customer-1", tableId: ""));
        _orders.Setup(repository => repository.GetOrderByIdAsync("order-1", "customer-1"))
            .ReturnsAsync(Order([]));
        _notifications.Setup(repository => repository.SaveNotificationAsync(It.IsAny<Notification>()))
            .Returns(Task.CompletedTask);

        var notification = await _service.CreateOrderNotificationAsync("waiter-1", "order-1", "reservation-1");

        Assert.Contains("Table 0", notification.Message);
        Assert.Contains("no dishes", notification.Message);
        _locations.Verify(repository => repository.GetTableByIdAsync(It.IsAny<string>()), Times.Never);
        _dishes.Verify(repository => repository.GetDishByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetUnreadNotificationsAsync_FiltersReadAndLimitsToTwenty()
    {
        var notifications = Enumerable.Range(1, 25).Select(index => new Notification
        {
            Id = index.ToString(),
            UserId = "waiter-1",
            Message = "Order",
            IsRead = index == 1
        }).ToList();
        _notifications.Setup(repository => repository.GetUnreadNotificationsByUserIdAsync("waiter-1"))
            .ReturnsAsync(notifications);

        var result = (await _service.GetUnreadNotificationsAsync("waiter-1")).ToList();

        Assert.Equal(20, result.Count);
        Assert.All(result, notification => Assert.False(notification.IsRead));
        Assert.DoesNotContain(result, notification => notification.Id == "1");
    }

    [Fact]
    public async Task MarkAsReadAsync_NotificationExists_SetsReadAndSaves()
    {
        var notification = new Notification { Id = "notification-1", UserId = "waiter-1", Message = "Order" };
        _notifications.Setup(repository => repository.GetNotificationByIdAsync(notification.Id))
            .ReturnsAsync(notification);
        _notifications.Setup(repository => repository.SaveNotificationAsync(notification)).Returns(Task.CompletedTask);

        await _service.MarkAsReadAsync(notification.Id);

        Assert.True(notification.IsRead);
        _notifications.Verify(repository => repository.SaveNotificationAsync(notification), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_NotificationMissing_ThrowsInvalidOperation()
    {
        _notifications.Setup(repository => repository.GetNotificationByIdAsync("missing"))
            .ReturnsAsync((Notification?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.MarkAsReadAsync("missing"));
        _notifications.Verify(repository => repository.SaveNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task GetNotificationByIdAsync_DelegatesToRepository()
    {
        var notification = new Notification { Id = "notification-1", UserId = "waiter-1", Message = "Order" };
        _notifications.Setup(repository => repository.GetNotificationByIdAsync(notification.Id))
            .ReturnsAsync(notification);

        var result = await _service.GetNotificationByIdAsync(notification.Id);

        Assert.Same(notification, result);
    }

    private static Reservation Reservation(string? customerId, string tableId = "table-1") => new()
    {
        Id = "reservation-1",
        TableId = tableId,
        WaiterId = "waiter-1",
        CustomerId = customerId,
        StartTime = new DateTime(2026, 6, 1, 18, 30, 0, DateTimeKind.Utc),
        GuestCount = 3
    };

    private static Order Order(List<OrderItem> items) => new()
    {
        OrderId = "order-1",
        CustomerId = "customer-1",
        ReservationId = "reservation-1",
        ReservationAddress = "Central",
        ReservationTime = "6 PM",
        Dishes = items
    };

    private static Dish Dish(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Description = "Description",
        Weight = "300g",
        Calories = "200",
        Proteins = "7g",
        Fats = "4g",
        Carbohydrates = "25g",
        Vitamins = "A",
        LocationId = "location-1"
    };
}
