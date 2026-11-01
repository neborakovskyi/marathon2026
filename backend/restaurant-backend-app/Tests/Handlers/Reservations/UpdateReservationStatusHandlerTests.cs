using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Handlers.Reservations;
using APIHandler.Models.Feedbacks;
using APIHandler.Models.Reports;
using APIHandler.Services.Feedbacks;
using APIHandler.Services.Orders;
using APIHandler.Services.Reports.Interfaces;
using APIHandler.Services.Reservations;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Reservations;

public class UpdateReservationStatusHandlerTests
{
    private readonly Mock<IReservationService> _reservations = new();
    private readonly Mock<IOrderService> _orders = new();
    private readonly Mock<IFeedbackService> _feedback = new();
    private readonly Mock<IReportService> _reports = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public UpdateReservationStatusHandlerTests() => _context.SetupGet(c => c.Logger).Returns(_logger.Object);

    [Fact]
    public async Task HandleRequest_MissingReservationId_ReturnsBadRequestWithoutServiceCall()
    {
        var response = await Handle(new APIGatewayProxyRequest { RequestContext = WaiterContext() });

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task HandleRequest_InvalidOrNullBody_ReturnsBadRequest(string body)
    {
        var response = await Handle(Request("reservation-1", body, waiter: true));

        Assert.Equal(400, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_MissingWaiterGroup_ReturnsForbiddenWithoutLoadingReservation()
    {
        var response = await Handle(Request("reservation-1", Body(ReservationStatus.InProgress), waiter: false));

        Assert.Equal(403, response.StatusCode);
        _reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_ReservationNotFound_ReturnsNotFound()
    {
        _reservations.Setup(s => s.GetReservationByIdAsync("reservation-1")).ReturnsAsync((Reservation?)null);

        var response = await Handle(Request("reservation-1", Body(ReservationStatus.InProgress)));

        Assert.Equal(404, response.StatusCode);
        _reservations.Verify(s => s.UpdateReservationAsync(It.IsAny<Reservation>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_ReservedTarget_ReturnsBadRequestWithoutUpdating()
    {
        SetupReservation(ReservationStatus.InProgress);

        var response = await Handle(Request("reservation-1", Body(ReservationStatus.Reserved)));

        Assert.Equal(400, response.StatusCode);
        _reservations.Verify(s => s.UpdateReservationAsync(It.IsAny<Reservation>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_CompletedWithoutFeedback_ReturnsBadRequest()
    {
        SetupReservation(ReservationStatus.InProgress);
        _feedback.Setup(s => s.GetFeedbackForReservationAsync("reservation-1")).ReturnsAsync([]);

        var response = await Handle(Request("reservation-1", Body(ReservationStatus.Completed)));

        Assert.Equal(400, response.StatusCode);
        _reservations.Verify(s => s.UpdateReservationAsync(It.IsAny<Reservation>()), Times.Never);
        _reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_NonCompletedStatusUpdated_ReturnsSuccessWithoutGeneratingReport()
    {
        var reservation = SetupReservation(ReservationStatus.Reserved);
        _reservations.Setup(s => s.UpdateReservationAsync(reservation)).ReturnsAsync(true);

        var response = await Handle(Request("reservation-1", Body(ReservationStatus.InProgress)));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(ReservationStatus.InProgress, reservation.Status);
        Assert.Contains("successfully", response.Body);
        _reports.VerifyNoOtherCalls();
        _feedback.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_CompletedWithFeedback_UpdatesAndSendsReport()
    {
        var reservation = SetupReservation(ReservationStatus.InProgress);
        _feedback.Setup(s => s.GetFeedbackForReservationAsync("reservation-1")).ReturnsAsync(
            [new FeedbackResponseItem { ReservationId = "reservation-1", LocationId = "loc-1", Rating = 5, Type = FeedbackType.Service }]);
        _reservations.Setup(s => s.UpdateReservationAsync(reservation)).ReturnsAsync(true);
        var report = new Report { ReservationId = "reservation-1", WaiterId = "waiter-1", LocationId = "loc-1" };
        _reports.Setup(s => s.GenerateReportAsync("reservation-1")).ReturnsAsync(report);
        _reports.Setup(s => s.SendReportToSqsAsync(report)).Returns(Task.CompletedTask);

        var response = await Handle(Request("reservation-1", Body(ReservationStatus.Completed)));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(ReservationStatus.Completed, reservation.Status);
        _reports.Verify(s => s.GenerateReportAsync("reservation-1"), Times.Once);
        _reports.Verify(s => s.SendReportToSqsAsync(report), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_UpdateFailure_ReturnsServerErrorAndDoesNotGenerateReport()
    {
        var reservation = SetupReservation(ReservationStatus.Reserved);
        _reservations.Setup(s => s.UpdateReservationAsync(reservation)).ReturnsAsync(false);

        var response = await Handle(Request("reservation-1", Body(ReservationStatus.InProgress)));

        Assert.Equal(500, response.StatusCode);
        _reports.VerifyNoOtherCalls();
    }

    private Reservation SetupReservation(ReservationStatus status)
    {
        var reservation = new Reservation
        {
            Id = "reservation-1", TableId = "table-1", WaiterId = "waiter-1",
            Status = status, GuestCount = 2
        };
        _reservations.Setup(s => s.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        return reservation;
    }

    private Task<APIGatewayProxyResponse> Handle(APIGatewayProxyRequest request) =>
        new UpdateReservationStatusHandler(_reservations.Object, _orders.Object, _feedback.Object, _reports.Object)
            .HandleRequest(request, _context.Object);

    private static APIGatewayProxyRequest Request(string id, string body, bool waiter = true) => new()
    {
        PathParameters = new Dictionary<string, string> { ["reservationId"] = id },
        Body = body,
        RequestContext = WaiterContext(waiter)
    };

    private static APIGatewayProxyRequest.ProxyRequestContext WaiterContext(bool waiter = true) => new()
    {
        Authorizer = new APIGatewayCustomAuthorizerContext
        {
            Claims = waiter
                ? new Dictionary<string, string> { ["cognito:groups"] = "Waiter" }
                : new Dictionary<string, string> { ["cognito:groups"] = "Customer" }
        }
    };

    private static string Body(ReservationStatus status) => JsonSerializer.Serialize(new { newStatus = status });
}
