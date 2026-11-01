using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Handlers.Reservations;
using APIHandler.Models.WaiterList;
using APIHandler.Services.Reservations;
using APIHandler.Services.Waiters;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Reservations;

public class CancelReservationHandlerTests
{
    private readonly Mock<IWaiterService> _waiters = new();
    private readonly Mock<IReservationService> _reservations = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public CancelReservationHandlerTests() => _context.SetupGet(c => c.Logger).Returns(_logger.Object);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    public async Task HandleRequest_EmptyOrNullBody_ReturnsBadRequest(string? body)
    {
        var response = await Handle(body, null);

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_MissingReservationId_ReturnsBadRequest()
    {
        var response = await Handle("""{"id":" "}""", Claims("customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_MissingIdentity_ReturnsUnauthorized()
    {
        var response = await Handle(Body(), new Dictionary<string, string>());

        Assert.Equal(401, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_ReservationNotFound_ReturnsNotFound()
    {
        _reservations.Setup(s => s.GetReservationByIdAsync("reservation-1")).ReturnsAsync((Reservation?)null);

        var response = await Handle(Body(), Claims("customer-1", "Customer"));

        Assert.Equal(404, response.StatusCode);
    }

    [Theory]
    [InlineData("other-customer", "Customer")]
    [InlineData("other-waiter", "Waiter")]
    public async Task HandleRequest_NonOwnerCaller_ReturnsForbidden(string callerId, string role)
    {
        _reservations.Setup(s => s.GetReservationByIdAsync("reservation-1")).ReturnsAsync(Reservation());

        var response = await Handle(Body(), Claims(callerId, role));

        Assert.Equal(403, response.StatusCode);
        _reservations.Verify(s => s.UpdateReservationAsync(It.IsAny<Reservation>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_ReservationStartsWithinThirtyMinutes_ReturnsBadRequest()
    {
        var reservation = Reservation();
        reservation.StartTime = DateTime.UtcNow.AddMinutes(10);
        _reservations.Setup(s => s.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);

        var response = await Handle(Body(), Claims("customer-1", "Customer"));

        Assert.Equal(400, response.StatusCode);
        _reservations.Verify(s => s.UpdateReservationAsync(It.IsAny<Reservation>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_OwnerCancelsReservation_RemovesItFromAssignedWaiter()
    {
        var reservation = Reservation();
        _reservations.Setup(s => s.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _reservations.Setup(s => s.UpdateReservationAsync(reservation)).ReturnsAsync(true);
        _waiters.Setup(s => s.FindWaiterByReservationIdAsync(reservation.Id)).ReturnsAsync(new WaiterListItem
        {
            UserId = "waiter-1",
            LocationId = "location-1",
            Reservations = ["reservation-1", "reservation-2"]
        });

        var response = await Handle(Body(), Claims("customer-1", "Customer"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(APIHandler.Constants.ReservationStatus.Cancelled, reservation.Status);
        _reservations.Verify(s => s.UpdateReservationAsync(reservation), Times.Once);
        _waiters.Verify(s => s.UpdateWaiterReservationsAsync("waiter-1",
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "reservation-2" }))), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_OwnerCancelsWithoutWaiterAssignment_StillReturnsSuccess()
    {
        var reservation = Reservation();
        _reservations.Setup(s => s.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _reservations.Setup(s => s.UpdateReservationAsync(reservation)).ReturnsAsync(true);
        _waiters.Setup(s => s.FindWaiterByReservationIdAsync(reservation.Id)).ReturnsAsync((WaiterListItem?)null);

        var response = await Handle(Body(), Claims("customer-1", "Customer"));

        Assert.Equal(200, response.StatusCode);
        _waiters.Verify(s => s.UpdateWaiterReservationsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    private Task<APIGatewayProxyResponse> Handle(string? body, Dictionary<string, string>? claims) =>
        new CancelReservationHandler(_waiters.Object, _reservations.Object)
            .HandleRequest(new APIGatewayProxyRequest
            {
                Body = body,
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
                {
                    Authorizer = claims is null ? null : new APIGatewayCustomAuthorizerContext { Claims = claims }
                }
            }, _context.Object);

    private static string Body() => JsonSerializer.Serialize(new APIHandler.Models.Reservations.CancelReservationRequest
    {
        ReservationId = "reservation-1"
    });

    private static Dictionary<string, string> Claims(string userId, string group) =>
        new() { ["sub"] = userId, ["cognito:groups"] = group };

    private static Reservation Reservation() => new()
    {
        Id = "reservation-1",
        TableId = "table-1",
        CustomerId = "customer-1",
        WaiterId = "waiter-1",
        StartTime = DateTime.UtcNow.AddHours(2),
        EndTime = DateTime.UtcNow.AddHours(3),
        Status = APIHandler.Constants.ReservationStatus.Reserved
    };
}
