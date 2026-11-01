using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Services;

public class GuestSessionManagementTests
{
    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly Mock<IDishRepository> _dishes = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITableRepository> _tables = new();
    private readonly Mock<IOrderRepository> _orders = new();

    private GuestSessionService CreateService() => new(
        _groups.Object, _sessions.Object, _dishes.Object, _reservations.Object,
        _users.Object, _tables.Object, _orders.Object);

    [Fact]
    public async Task GetGroupReservationStatusAsync_ReturnsCapacityStatusAndOwnerInclusiveParticipantCount()
    {
        var group = Group();
        _groups.Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId)).ReturnsAsync(group);
        _reservations.Setup(r => r.GetReservationByIdAsync(group.ReservationId))
            .ReturnsAsync(Reservation(group.ReservationId, "owner"));
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([Session("guest-1", group.GroupReservationId)]);

        var result = await CreateService().GetGroupReservationStatusAsync(group.GroupReservationId);

        Assert.Equal(2, result.CurrentParticipants);
        Assert.Equal(6, result.MaxCapacity);
        Assert.Equal(group.Status, result.Status);
    }

    [Fact]
    public async Task GetGroupReservationStatusAsync_MissingGroup_ThrowsGroupReservationNotFound()
    {
        _groups.Setup(r => r.GetGroupReservationByIdAsync("missing")).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            CreateService().GetGroupReservationStatusAsync("missing"));
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetGroupReservationStatusAsync_MissingReservation_ThrowsInvalidOperation()
    {
        var group = Group();
        _groups.Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId)).ReturnsAsync(group);
        _reservations.Setup(r => r.GetReservationByIdAsync(group.ReservationId)).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().GetGroupReservationStatusAsync(group.GroupReservationId));
        _sessions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAllSessionsAsync_OwnerWithMixedDishAvailability_ReturnsPricedParticipantSnapshot()
    {
        var group = Group();
        var reservation = Reservation(group.ReservationId, "owner");
        var table = new Table { Id = reservation.TableId, LocationId = "location-1", Capacity = 6, TableNumber = 3 };
        var session = new GuestSession
        {
            Id = "session-1",
            GroupReservationId = group.GroupReservationId,
            GuestName = "Guest",
            IsSubmitted = true,
            Dishes = [new OrderItem { DishId = "dish-1", Quantity = 2 }, new OrderItem { DishId = "missing-dish", Quantity = 9 }]
        };
        _groups.Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId)).ReturnsAsync(group);
        _reservations.Setup(r => r.GetReservationByIdAsync(group.ReservationId)).ReturnsAsync(reservation);
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId)).ReturnsAsync([session]);
        _dishes.Setup(r => r.GetDishesByIdsAsync(It.Is<IEnumerable<string>>(ids =>
            ids.Order().SequenceEqual(new[] { "dish-1", "missing-dish" })))).ReturnsAsync([Dish("dish-1", 2.5m)]);

        var result = await CreateService().GetAllSessionsAsync("owner", group.GroupReservationId);

        var participant = Assert.Single(result.Participants);
        Assert.Equal("session-1", participant.Id);
        Assert.Equal("Guest", participant.Name);
        Assert.True(participant.IsSubmitted);
        Assert.Single(participant.Items);
        Assert.Equal(2, participant.Items.Single().Quantity);
        Assert.Equal(5m, participant.Total);
        Assert.Equal(5m, result.Total);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_OwnerDeletesSessionAndReturnsRemainingSnapshot()
    {
        var group = Group();
        var reservation = Reservation(group.ReservationId, "owner");
        var toDelete = Session("delete-me", group.GroupReservationId);
        _groups.Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId)).ReturnsAsync(group);
        _reservations.Setup(r => r.GetReservationByIdAsync(group.ReservationId)).ReturnsAsync(reservation);
        _sessions.Setup(r => r.GetByGuestSessionId(toDelete.Id)).ReturnsAsync(toDelete);
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId)).ReturnsAsync([]);
        _sessions.Setup(r => r.DeleteAsync(toDelete)).Returns(Task.CompletedTask);
        _dishes.Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync([]);

        var result = await CreateService().DeleteGuestSessionAsync("owner", group.GroupReservationId, toDelete.Id);

        Assert.Empty(result.Participants);
        Assert.Equal(0m, result.Total);
        _sessions.Verify(r => r.DeleteAsync(toDelete), Times.Once);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_SessionFromAnotherGroup_ThrowsValidationWithoutDeleting()
    {
        var group = Group();
        var reservation = Reservation(group.ReservationId, "owner");
        var session = Session("session-1", "different-group");
        _groups.Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId)).ReturnsAsync(group);
        _reservations.Setup(r => r.GetReservationByIdAsync(group.ReservationId)).ReturnsAsync(reservation);
        _sessions.Setup(r => r.GetByGuestSessionId(session.Id)).ReturnsAsync(session);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().DeleteGuestSessionAsync("owner", group.GroupReservationId, session.Id));
        _sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_ExistingOwnerSession_ReturnsItsIdWithoutCreatingAnother()
    {
        var reservation = Reservation("reservation-1", "owner");
        var group = Group();
        var ownerSession = new GuestSession
        {
            Id = "owner-session",
            GroupReservationId = group.GroupReservationId,
            GuestName = "Owner",
            Dishes = [],
            IsOwnerSession = true,
            OwnerUserId = "owner"
        };
        _reservations.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _groups.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync(group);
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId)).ReturnsAsync([ownerSession]);

        var result = await CreateService().GetOwnerSessionIdAsync("owner", reservation.Id);

        Assert.Equal(ownerSession.Id, result.GuestSessionId);
        _sessions.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_NoOwnerSession_CreatesOwnerSessionAndReturnsPersistedId()
    {
        var reservation = Reservation("reservation-1", "owner");
        var group = Group();
        var storedSessions = new List<GuestSession>();
        _reservations.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _groups.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync(group);
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync(() => storedSessions.ToList());
        _sessions.Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(storedSessions.Add).Returns(Task.CompletedTask);

        var result = await CreateService().GetOwnerSessionIdAsync("owner", reservation.Id);

        var created = Assert.Single(storedSessions);
        Assert.Equal(created.Id, result.GuestSessionId);
        Assert.True(created.IsOwnerSession);
        Assert.Equal("owner", created.OwnerUserId);
        Assert.Equal("Owner", created.GuestName);
    }

    [Fact]
    public async Task GetOwnerSessionIdAsync_NonOwnerCaller_ThrowsForbidden()
    {
        _reservations.Setup(r => r.GetReservationByIdAsync("reservation-1"))
            .ReturnsAsync(Reservation("reservation-1", "different-user"));

        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            CreateService().GetOwnerSessionIdAsync("caller", "reservation-1"));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_CombinesSubmittedGuestDishesAndUpdatesOrderAndGroup()
    {
        var group = Group();
        var reservation = Reservation(group.ReservationId, "owner");
        var existingItem = new OrderItem { DishId = "dish-1", Quantity = 2, State = OrderItemState.Pending };
        var order = Order(reservation.Id, [existingItem]);
        _groups.Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId)).ReturnsAsync(group);
        _groups.Setup(r => r.SaveGroupReservationAsync(group)).Returns(Task.CompletedTask);
        _reservations.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId)).ReturnsAsync(
        [
            new GuestSession
            {
                Id = "guest-1", GroupReservationId = group.GroupReservationId, GuestName = "One",
                IsSubmitted = true, Dishes = [new OrderItem { DishId = "dish-1", Quantity = 3 }, new OrderItem { DishId = "dish-2", Quantity = 1 }]
            },
            new GuestSession
            {
                Id = "guest-2", GroupReservationId = group.GroupReservationId, GuestName = "Two",
                IsSubmitted = true, Dishes = [new OrderItem { DishId = "dish-1", Quantity = 4 }]
            },
            new GuestSession
            {
                Id = "owner", GroupReservationId = group.GroupReservationId, GuestName = "Owner",
                IsSubmitted = true, IsOwnerSession = true, Dishes = [new OrderItem { DishId = "ignored", Quantity = 10 }]
            }
        ]);
        _orders.Setup(r => r.GetOrderByReservationIdAsync(reservation.Id)).ReturnsAsync(order);
        _orders.Setup(r => r.SaveOrderAsync(order)).Returns(Task.CompletedTask);

        var result = (await CreateService().SubmitGroupReservationAsync("owner", group.GroupReservationId, reservation.Id)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.DishId == "dish-1" && item.Quantity == 7);
        Assert.Contains(result, item => item.DishId == "dish-2" && item.Quantity == 1);
        Assert.Equal(9, existingItem.Quantity);
        Assert.Contains(order.Dishes, item => item.DishId == "dish-2" && item.Quantity == 1);
        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(GroupReservationStatus.Submitted, group.Status);
        _orders.Verify(r => r.SaveOrderAsync(order), Times.Once);
        _groups.Verify(r => r.SaveGroupReservationAsync(group), Times.Once);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_NoSubmittedGuestSessions_ThrowsValidation()
    {
        var group = Group();
        _groups.Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId)).ReturnsAsync(group);
        _reservations.Setup(r => r.GetReservationByIdAsync(group.ReservationId))
            .ReturnsAsync(Reservation(group.ReservationId, "owner"));
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId)).ReturnsAsync([]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().SubmitGroupReservationAsync("owner", group.GroupReservationId, group.ReservationId));
        _orders.VerifyNoOtherCalls();
    }

    private static GroupReservation Group() => new()
    {
        GroupReservationId = "group-1",
        ReservationId = "reservation-1",
        ReservationToken = "token",
        Status = GroupReservationStatus.Open
    };

    private static Reservation Reservation(string id, string ownerId) => new()
    {
        Id = id,
        TableId = "table-1",
        WaiterId = "waiter-1",
        CustomerId = ownerId,
        GuestCount = 6
    };

    private static GuestSession Session(string id, string groupId) => new()
    {
        Id = id,
        GroupReservationId = groupId,
        GuestName = id,
        Dishes = []
    };

    private static Dish Dish(string id, decimal price) => new()
    {
        Id = id,
        Name = id,
        Description = "Description",
        Price = price,
        Weight = "100g",
        Calories = "100",
        Proteins = "1g",
        Fats = "1g",
        Carbohydrates = "1g",
        Vitamins = "A",
        LocationId = "location-1"
    };

    private static Order Order(string reservationId, List<OrderItem> items) => new()
    {
        OrderId = "order-1",
        CustomerId = "owner",
        ReservationId = reservationId,
        ReservationAddress = "Central",
        ReservationTime = "Today",
        Dishes = items,
        Status = OrderStatus.Created
    };
}
