using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.Reservations;
using APIHandler.Models.Reservations;
using APIHandler.Services.Reservations;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Reservations;

public class UpdateReservationTableHandlerTests
{
    private readonly Mock<IReservationService> _reservations = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public UpdateReservationTableHandlerTests() => _context.SetupGet(c => c.Logger).Returns(_logger.Object);

    [Fact]
    public async Task HandleRequest_MissingUserIdentity_ReturnsUnauthorized()
    {
        var response = await Handle(Request(null, "reservation-1", """{"tableId":"table-2"}"""));

        Assert.Equal(401, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("")]
    public async Task HandleRequest_NonWaiterCaller_ReturnsForbidden(string groups)
    {
        var response = await Handle(Request("user-1", "reservation-1", """{"tableId":"table-2"}""", groups));

        Assert.Equal(403, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_MissingReservationId_ReturnsBadRequest()
    {
        var response = await Handle(Request("waiter-1", null, """{"tableId":"table-2"}"""));

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task HandleRequest_MissingBody_ReturnsBadRequest(string? body)
    {
        var response = await Handle(Request("waiter-1", "reservation-1", body));

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{")]
    public async Task HandleRequest_MalformedBody_ReturnsBadRequest(string body)
    {
        var response = await Handle(Request("waiter-1", "reservation-1", body));

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_MissingTableId_ReturnsBadRequest()
    {
        var response = await Handle(Request("waiter-1", "reservation-1", "{}"));

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_ValidRequest_ReturnsReservationResponseAndCallsService()
    {
        var expected = Response();
        _reservations.Setup(s => s.UpdateReservationTableAsync("waiter-1", "reservation-1", "table-2", _logger.Object))
            .ReturnsAsync(expected);

        var response = await Handle(Request("waiter-1", "reservation-1", """{"tableId":"table-2"}"""));

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("table-2", response.Body);
        _reservations.Verify(s => s.UpdateReservationTableAsync("waiter-1", "reservation-1", "table-2", _logger.Object), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_ReservationNotFound_MapsToNotFound()
    {
        _reservations.Setup(s => s.UpdateReservationTableAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILambdaLogger>()))
            .ThrowsAsync(new ReservationNotFoundException("Reservation not found."));

        var response = await Handle(Request("waiter-1", "reservation-1", """{"tableId":"table-2"}"""));

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Reservation not found", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ValidationFailure_MapsToBadRequest()
    {
        _reservations.Setup(s => s.UpdateReservationTableAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILambdaLogger>()))
            .ThrowsAsync(new ValidationException("Table is unavailable."));

        var response = await Handle(Request("waiter-1", "reservation-1", """{"tableId":"table-2"}"""));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("unavailable", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ForbiddenServiceFailure_MapsToForbidden()
    {
        _reservations.Setup(s => s.UpdateReservationTableAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILambdaLogger>()))
            .ThrowsAsync(new ActionForbiddenException("Not your reservation."));

        var response = await Handle(Request("waiter-1", "reservation-1", """{"tableId":"table-2"}"""));

        Assert.Equal(403, response.StatusCode);
        Assert.Contains("Not your reservation", response.Body);
    }

    private Task<APIGatewayProxyResponse> Handle(APIGatewayProxyRequest request) =>
        new UpdateReservationTableHandler(_reservations.Object).HandleRequest(request, _context.Object);

    private static APIGatewayProxyRequest Request(string? userId, string? reservationId, string? body, string groups = "Waiter") =>
        new()
        {
            PathParameters = reservationId is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["reservationId"] = reservationId },
            Body = body,
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = userId is null
                    ? null
                    : new APIGatewayCustomAuthorizerContext
                    {
                        Claims = new Dictionary<string, string>
                        {
                            ["sub"] = userId,
                            ["cognito:groups"] = groups
                        }
                    }
            }
        };

    private static ReservationResponse Response() => new()
    {
        Id = "reservation-1",
        TableId = "table-2",
        StartTime = DateTime.UtcNow.AddHours(2),
        EndTime = DateTime.UtcNow.AddHours(3),
        GuestCount = 2,
        WaiterId = "waiter-1",
        Address = "Central",
        RestaurantName = "Green & Tasty",
        Status = APIHandler.Constants.ReservationStatus.Reserved,
        CreatedDate = DateTime.UtcNow
    };
}
