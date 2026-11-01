using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Handlers.Reservations;
using APIHandler.Models.Reservations;
using APIHandler.Services.Locations;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Reservations;

[Collection("ResourceNamesEnvironment")]
public class EditReservationHandlerTests
{
    private readonly Mock<IDynamoDBContext> _db = new();
    private readonly Mock<ILocationService> _locations = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public EditReservationHandlerTests()
    {
        _context.SetupGet(c => c.Logger).Returns(_logger.Object);
        Environment.SetEnvironmentVariable("reservationsTable", "reservations-for-tests");
        Environment.SetEnvironmentVariable("tablesTable", "tables-for-tests");
        Environment.SetEnvironmentVariable("reservationsTableGlobalIndex", "table-index-for-tests");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task HandleRequest_InvalidBody_ReturnsBadRequest(string body)
    {
        var response = await Handle(body, null);

        Assert.Equal(400, response.StatusCode);
        _db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_MissingIdentity_ReturnsUnauthorized()
    {
        var response = await Handle(Body(), null);

        Assert.Equal(401, response.StatusCode);
        _db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_ReservationMissing_ReturnsNotFound()
    {
        _db.Setup(db => db.LoadAsync<Reservation>("reservation-1", It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reservation?)null);

        var response = await Handle(Body(), Claims("customer-1", "Customer"));

        Assert.Equal(404, response.StatusCode);
        _db.Verify(db => db.LoadAsync<Table>(It.IsAny<object>(), It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("customer-2", "Customer")]
    [InlineData("waiter-2", "Waiter")]
    [InlineData("admin-1", "Admin")]
    public async Task HandleRequest_UserWithoutOwnership_ReturnsForbidden(string caller, string role)
    {
        SetupReservation();

        var response = await Handle(Body(), Claims(caller, role));

        Assert.Equal(403, response.StatusCode);
        _db.Verify(db => db.LoadAsync<Table>(It.IsAny<object>(), It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_StartLessThanFifteenMinutesAway_ReturnsBadRequest()
    {
        SetupReservation();

        var response = await Handle(Body(DateTime.UtcNow.AddMinutes(5), DateTime.UtcNow.AddMinutes(95)), Claims("customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("15 minutes", response.Body);
    }

    [Fact]
    public async Task HandleRequest_EndNotAfterStart_ReturnsBadRequest()
    {
        SetupReservation();
        var start = DateTime.UtcNow.AddHours(3);

        var response = await Handle(Body(start, start), Claims("customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("End time", response.Body);
    }

    [Fact]
    public async Task HandleRequest_TableMissing_ReturnsNotFound()
    {
        SetupReservation();
        _db.Setup(db => db.LoadAsync<Table>("table-1", It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Table?)null);

        var response = await Handle(Body(), Claims("customer-1", "Customer"));

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Table not found", response.Body);
    }

    [Fact]
    public async Task HandleRequest_GuestCountExceedsTableCapacity_ReturnsBadRequest()
    {
        SetupReservation();
        SetupTable(capacity: 3);

        var response = await Handle(Body(guestCount: 4), Claims("customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("capacity", response.Body);
        _db.Verify(db => db.QueryAsync<Reservation>(It.IsAny<object>(), It.IsAny<QueryConfig>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_OverlappingReservation_ReturnsConflict()
    {
        SetupReservation();
        SetupTable();
        SetupReservationQuery(
        [
            ExistingReservation("other", DateTime.UtcNow.AddHours(3), DateTime.UtcNow.AddHours(4))
        ]);

        var start = DateTime.UtcNow.AddHours(3).AddMinutes(30);
        var response = await Handle(Body(start, start.AddMinutes(90)), Claims("customer-1", "Customer"));

        Assert.Equal(409, response.StatusCode);
        _db.Verify(db => db.SaveAsync(It.IsAny<Reservation>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_CancelledOverlapIsIgnored_UpdatesReservation()
    {
        var original = SetupReservation();
        SetupTable();
        SetupReservationQuery(
        [
            ExistingReservation("cancelled", DateTime.UtcNow.AddHours(3), DateTime.UtcNow.AddHours(4), APIHandler.Constants.ReservationStatus.Cancelled)
        ]);
        _db.Setup(db => db.SaveAsync(original, It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _locations.Setup(s => s.GetLocationByTableIdAsync("table-1")).ReturnsAsync(Location());
        var start = DateTime.UtcNow.AddHours(3).AddMinutes(30);

        var response = await Handle(Body(start, start.AddMinutes(90), guestCount: 2), Claims("customer-1", "Customer"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(start, original.StartTime);
        Assert.Equal(start.AddMinutes(90), original.EndTime);
        Assert.Equal(2, original.GuestCount);
        _db.Verify(db => db.SaveAsync(original, It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_WaiterCanEditOwnedReservation_ReturnsUpdatedReservation()
    {
        var reservation = SetupReservation();
        SetupTable(capacity: 5);
        SetupReservationQuery([]);
        _db.Setup(db => db.SaveAsync(reservation, It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _locations.Setup(s => s.GetLocationByTableIdAsync("table-1")).ReturnsAsync(Location());
        var start = DateTime.UtcNow.AddHours(5);

        var response = await Handle(Body(start, start.AddMinutes(75), guestCount: 3),
            Claims("waiter-1", "Customer,Waiter"));

        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal("reservation-1", body.RootElement.GetProperty("id").GetString());
        Assert.Equal("Central", body.RootElement.GetProperty("locationAddress").GetString());
        Assert.Equal(3, body.RootElement.GetProperty("guestCount").GetInt32());
    }

    [Fact]
    public async Task HandleRequest_SaveFails_ReturnsServerError()
    {
        var reservation = SetupReservation();
        SetupTable();
        SetupReservationQuery([]);
        _db.Setup(db => db.SaveAsync(reservation, It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("write failed"));
        var start = DateTime.UtcNow.AddHours(4);

        var response = await Handle(Body(start, start.AddMinutes(60)), Claims("customer-1", "Customer"));

        Assert.Equal(500, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_LocationLookupFailure_ReturnsNotFoundAfterSaving()
    {
        var reservation = SetupReservation();
        SetupTable();
        SetupReservationQuery([]);
        _db.Setup(db => db.SaveAsync(reservation, It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _locations.Setup(s => s.GetLocationByTableIdAsync("table-1"))
            .ThrowsAsync(new InvalidOperationException("location unavailable"));
        var start = DateTime.UtcNow.AddHours(4);

        var response = await Handle(Body(start, start.AddMinutes(60)), Claims("customer-1", "Customer"));

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Location not foun.d", response.Body);
        _db.Verify(db => db.SaveAsync(reservation, It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private Reservation SetupReservation()
    {
        var reservation = new Reservation
        {
            Id = "reservation-1", TableId = "table-1", CustomerId = "customer-1", WaiterId = "waiter-1",
            StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            GuestCount = 2, Status = APIHandler.Constants.ReservationStatus.Reserved
        };
        _db.Setup(db => db.LoadAsync<Reservation>(reservation.Id, It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        return reservation;
    }

    private void SetupTable(int capacity = 6) =>
        _db.Setup(db => db.LoadAsync<Table>("table-1", It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Table { Id = "table-1", LocationId = "location-1", Capacity = capacity, TableNumber = 1 });

    private void SetupReservationQuery(List<Reservation> reservations)
    {
        var search = new Mock<IAsyncSearch<Reservation>>();
        search.Setup(s => s.GetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(reservations);
        _db.Setup(db => db.QueryAsync<Reservation>("table-1", It.IsAny<QueryConfig>())).Returns(search.Object);
    }

    private Task<APIGatewayProxyResponse> Handle(string body, Dictionary<string, string>? claims) =>
        new EditReservationHandler(_db.Object, _locations.Object)
            .HandleRequest(new APIGatewayProxyRequest
            {
                Body = body,
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
                {
                    Authorizer = claims is null ? null : new APIGatewayCustomAuthorizerContext { Claims = claims }
                }
            }, _context.Object);

    private static Dictionary<string, string> Claims(string userId, string role) =>
        new() { ["sub"] = userId, ["cognito:groups"] = role };

    private static string Body(DateTime? startTime = null, DateTime? endTime = null, int guestCount = 2) =>
        JsonSerializer.Serialize(new EditReservationRequest
        {
            ReservationId = "reservation-1",
            StartTime = startTime ?? DateTime.UtcNow.AddHours(5),
            EndTime = endTime ?? DateTime.UtcNow.AddHours(6),
            GuestCount = guestCount
        });

    private static Reservation ExistingReservation(
        string id, DateTime start, DateTime end,
        APIHandler.Constants.ReservationStatus status = APIHandler.Constants.ReservationStatus.Reserved) => new()
    {
        Id = id, TableId = "table-1", WaiterId = "waiter-2",
        StartTime = start, EndTime = end, Status = status
    };

    private static APIHandler.Entities.Location Location() => new()
    {
        Id = "location-1", Address = "Central", Description = "Restaurant", AverageOccupancy = "medium"
    };
}
