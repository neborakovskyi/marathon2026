using Amazon;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using APIHandler.Constants;
using APIHandler.Constants.Reports;
using APIHandler.Entities;
using APIHandler.Entities.Reports;
using APIHandler.Exceptions;
using APIHandler.Models.Feedbacks;
using APIHandler.Models.Reports;
using APIHandler.Repositories.Reports;
using APIHandler.Services.Auth;
using APIHandler.Services.Dishes;
using APIHandler.Services.Feedbacks;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using APIHandler.Services.Reports;
using APIHandler.Services.Reservations;
using APIHandler.Services.Waiters;
using Moq;
using System.Net;

namespace APIHandler.Tests.Services;

[Collection("ResourceNamesEnvironment")]
public class ReportServiceTests
{
    private static readonly DateOnly Start = new(2026, 5, 11);
    private static readonly DateOnly End = new(2026, 5, 17);
    private readonly Mock<IReservationService> _reservations = new();
    private readonly Mock<IWaiterService> _waiters = new();
    private readonly Mock<IFeedbackService> _feedback = new();
    private readonly Mock<IOrderService> _orders = new();
    private readonly Mock<IDishService> _dishes = new();
    private readonly Mock<IUserService> _users = new();
    private readonly Mock<ILocationService> _locations = new();
    private readonly Mock<IReportRepository> _repository = new();
    private readonly Mock<AmazonSQSClient> _sqs = new(RegionEndpoint.USWest2);

    private ReportService CreateService()
    {
        var oldQueueName = Environment.GetEnvironmentVariable("reports_queue_name");
        Environment.SetEnvironmentVariable("reports_queue_name", "unit-test-reports");
        try
        {
            return new ReportService(
                _reservations.Object,
                _waiters.Object,
                _feedback.Object,
                _orders.Object,
                _dishes.Object,
                _users.Object,
                _locations.Object,
                _sqs.Object,
                _repository.Object);
        }
        finally
        {
            Environment.SetEnvironmentVariable("reports_queue_name", oldQueueName);
        }
    }

    [Fact]
    public async Task GenerateReportAsync_CompletedReservationWithFeedback_MapsReservationWaiterAndRatings()
    {
        var reservation = new Reservation
        {
            Id = "reservation-1",
            TableId = "table-1",
            WaiterId = "waiter-1",
            StartTime = new DateTime(2026, 5, 10, 18, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 10, 19, 30, 0, DateTimeKind.Utc),
            Status = ReservationStatus.Completed
        };
        _reservations.Setup(s => s.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _waiters.Setup(s => s.GetWaiterByReservationIdAsync(reservation.Id))
            .ReturnsAsync(new APIHandler.Models.WaiterList.WaiterListItem
            {
                UserId = "waiter-1",
                LocationId = "location-1"
            });
        _feedback.Setup(s => s.GetFeedbackForReservationAsync(reservation.Id)).ReturnsAsync(
        [
            Feedback(reservation.Id, FeedbackType.Service, 5),
            Feedback(reservation.Id, FeedbackType.CuisineExperience, 4)
        ]);

        var report = await CreateService().GenerateReportAsync(reservation.Id);

        Assert.Equal(reservation.Id, report.ReservationId);
        Assert.Equal(reservation.StartTime, report.ReservationStart);
        Assert.Equal(reservation.EndTime, report.ReservationEnd);
        Assert.Equal("waiter-1", report.WaiterId);
        Assert.Equal("location-1", report.LocationId);
        Assert.Equal(5, report.ServiceRating);
        Assert.Equal(4, report.CuisineRating);
        Assert.Equal(0m, report.Revenue);
    }

    [Fact]
    public async Task GenerateReportAsync_ReservationNotFound_ThrowsArgumentException()
    {
        _reservations.Setup(s => s.GetReservationByIdAsync("missing")).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().GenerateReportAsync("missing"));
        _waiters.VerifyNoOtherCalls();
        _feedback.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateReportAsync_ReservationNotCompleted_ThrowsValidationException()
    {
        var reservation = ReservationWithStatus(ReservationStatus.InProgress);
        _reservations.Setup(s => s.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => CreateService().GenerateReportAsync(reservation.Id));
        _waiters.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateReportAsync_WaiterNotAssigned_ThrowsValidationException()
    {
        var reservation = ReservationWithStatus(ReservationStatus.Completed);
        _reservations.Setup(s => s.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _waiters.Setup(s => s.GetWaiterByReservationIdAsync(reservation.Id)).ReturnsAsync((APIHandler.Models.WaiterList.WaiterListItem?)null);

        await Assert.ThrowsAsync<ValidationException>(() => CreateService().GenerateReportAsync(reservation.Id));
        _feedback.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateReportAsync_FeedbackMissing_ThrowsValidationException()
    {
        var reservation = ReservationWithStatus(ReservationStatus.Completed);
        _reservations.Setup(s => s.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _waiters.Setup(s => s.GetWaiterByReservationIdAsync(reservation.Id)).ReturnsAsync(
            new APIHandler.Models.WaiterList.WaiterListItem { UserId = "waiter", LocationId = "location" });
        _feedback.Setup(s => s.GetFeedbackForReservationAsync(reservation.Id)).ReturnsAsync((List<FeedbackResponseItem>?)null!);

        await Assert.ThrowsAsync<ValidationException>(() => CreateService().GenerateReportAsync(reservation.Id));
    }

    [Fact]
    public async Task GenerateReportAsync_FeedbackTypesMissing_UsesZeroRatings()
    {
        var reservation = ReservationWithStatus(ReservationStatus.Completed);
        _reservations.Setup(s => s.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        _waiters.Setup(s => s.GetWaiterByReservationIdAsync(reservation.Id)).ReturnsAsync(
            new APIHandler.Models.WaiterList.WaiterListItem { UserId = "waiter", LocationId = "location" });
        _feedback.Setup(s => s.GetFeedbackForReservationAsync(reservation.Id)).ReturnsAsync([]);

        var report = await CreateService().GenerateReportAsync(reservation.Id);

        Assert.Equal(0, report.ServiceRating);
        Assert.Equal(0, report.CuisineRating);
    }

    [Fact]
    public async Task SendReportToSqsAsync_QueueAvailable_SendsSerializedReport()
    {
        _sqs.Setup(s => s.GetQueueUrlAsync("unit-test-reports", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = "https://sqs.us-west-2.amazonaws.com/123/unit-test-reports" });
        SendMessageRequest? sent = null;
        _sqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(new SendMessageResponse { HttpStatusCode = HttpStatusCode.OK });
        var report = NewReport();

        await CreateService().SendReportToSqsAsync(report);

        Assert.NotNull(sent);
        Assert.Equal("https://sqs.us-west-2.amazonaws.com/123/unit-test-reports", sent.QueueUrl);
        Assert.Equal(report.ReservationId, System.Text.Json.JsonDocument.Parse(sent.MessageBody).RootElement.GetProperty("ReservationId").GetString());
    }

    [Fact]
    public async Task SendReportToSqsAsync_QueueUrlMissing_ThrowsInvalidOperationException()
    {
        _sqs.Setup(s => s.GetQueueUrlAsync("unit-test-reports", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = null });

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().SendReportToSqsAsync(NewReport()));
        _sqs.Verify(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendReportToSqsAsync_SendRejected_ThrowsInvalidOperationException()
    {
        _sqs.Setup(s => s.GetQueueUrlAsync("unit-test-reports", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = "queue-url" });
        _sqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { HttpStatusCode = HttpStatusCode.ServiceUnavailable });

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().SendReportToSqsAsync(NewReport()));
    }

    [Fact]
    public async Task GetAggregatesForPeriodAsync_ConvertsDatesToFullInclusiveDaysAndPassesLocation()
    {
        List<DailyAggregate> expected = [Aggregate("waiter", "location", 1, 2, 3, 4, 5, 6, 7)];
        _repository.Setup(r => r.GetAggregatesForPeriodAsync(
                Start.ToDateTime(TimeOnly.MinValue), End.ToDateTime(TimeOnly.MaxValue), "location"))
            .ReturnsAsync(expected);

        var result = await CreateService().GetAggregatesForPeriodAsync(Start, End, "location");

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GenerateWaiterReportsAsync_GroupsAggregateDataAndCalculatesDeltas()
    {
        var current = new List<DailyAggregate>
        {
            Aggregate("waiter-1", "location-1", 4, 3, 5, 2, 100, 4, 10),
            Aggregate("waiter-1", "location-1", 5, 5, 7, 4, 200, 6, 20)
        };
        var previous = new List<DailyAggregate>
        {
            Aggregate("waiter-1", "location-1", 5, 1, 3, 1, 50, 2, 15)
        };
        SetupAggregates(previous);
        _locations.Setup(s => s.GetLocationByIdAsync("location-1")).ReturnsAsync(NewLocation("Central"));
        _users.Setup(s => s.GetUserName("waiter-1")).ReturnsAsync("Jordan");
        _users.Setup(s => s.GetUserEmail("waiter-1")).ReturnsAsync("jordan@example.com");

        var result = await CreateService().GenerateWaiterReportsAsync(current, Start, End, "user-1", new Mock<ILambdaLogger>().Object);

        var report = Assert.Single(result);
        Assert.Equal("Jordan", report.WaiterName);
        Assert.Equal("jordan@example.com", report.WaiterEmail);
        Assert.Equal("Central", report.LocationName);
        Assert.Equal(30, report.TotalOrders);
        Assert.Equal(10, report.WorkingHours);
        Assert.Equal(4.5, report.AvgServiceFeedback);
        Assert.Equal(3, report.MinServiceFeedback);
        Assert.Equal(90, report.DeltaAvgServiceFeedback);
        Assert.Equal(200, report.DeltaTotalOrders);
        Assert.Equal(ReportType.Service, report.ReportType);
        Assert.Equal(Start, report.StartDate);
        Assert.Equal(End, report.EndDate);
        Assert.Equal("user-1", report.UserId);
    }

    [Fact]
    public async Task GenerateWaiterReportsAsync_NoPreviousValuesOrIdentity_UsesZeroDeltasAndUnknownLabels()
    {
        SetupAggregates([]);
        _locations.Setup(s => s.GetLocationByIdAsync("location-1")).ReturnsAsync((Location?)null);
        _users.Setup(s => s.GetUserName("waiter-1")).ReturnsAsync((string?)null);
        _users.Setup(s => s.GetUserEmail("waiter-1")).ReturnsAsync((string?)null);

        var result = await CreateService().GenerateWaiterReportsAsync(
            [Aggregate("waiter-1", "location-1", 2, 1, 3, 1, 10, 1, 2)],
            Start, End, "user-1", new Mock<ILambdaLogger>().Object);

        var report = Assert.Single(result);
        Assert.Equal("Unknown", report.WaiterName);
        Assert.Equal("Unknown", report.WaiterEmail);
        Assert.Equal("Unknown", report.LocationName);
        Assert.Equal(0, report.DeltaAvgServiceFeedback);
        Assert.Equal(0, report.DeltaTotalOrders);
    }

    [Fact]
    public async Task GenerateWaiterReportsAsync_PreviousZeroDenominators_UsesZeroDeltas()
    {
        SetupAggregates([Aggregate("waiter-1", "location-1", 0, 1, 0, 1, 0, 1, 0)]);
        _locations.Setup(s => s.GetLocationByIdAsync("location-1")).ReturnsAsync(NewLocation("Central"));
        _users.Setup(s => s.GetUserName("waiter-1")).ReturnsAsync("Jordan");
        _users.Setup(s => s.GetUserEmail("waiter-1")).ReturnsAsync("jordan@example.com");

        var result = await CreateService().GenerateWaiterReportsAsync(
            [Aggregate("waiter-1", "location-1", 2, 1, 3, 1, 10, 1, 2)],
            Start, End, "user-1", new Mock<ILambdaLogger>().Object);

        var report = Assert.Single(result);
        Assert.Equal(0, report.DeltaAvgServiceFeedback);
        Assert.Equal(0, report.DeltaTotalOrders);
    }

    [Fact]
    public async Task GenerateLocationReportsAsync_GroupsAggregateDataAndComputesPreviousPeriodChanges()
    {
        SetupAggregates([Aggregate("waiter-1", "location-1", 1, 1, 3, 1, 100, 1, 5)]);
        _locations.Setup(s => s.GetLocationByIdAsync("location-1")).ReturnsAsync(NewLocation("Central"));

        var result = await CreateService().GenerateLocationReportsAsync(
            [
                Aggregate("waiter-1", "location-1", 2, 1, 5, 3, 150, 2, 10),
                Aggregate("waiter-2", "location-1", 4, 3, 3, 1, 250, 4, 20)
            ],
            Start, End, "user-1", new Mock<ILambdaLogger>().Object);

        var report = Assert.Single(result);
        Assert.Equal("Central", report.LocationName);
        Assert.Equal(30, report.TotalOrders);
        Assert.Equal(400m, report.Revenue);
        Assert.Equal(4, report.AvgCuisineFeedback);
        Assert.Equal(1, report.MinCuisineFeedback);
        Assert.Equal(300, report.DeltaRevenue);
        Assert.Equal(33.333333, report.DeltaAvgCuisineFeedback, 5);
        Assert.Equal(ReportType.Sales, report.ReportType);
    }

    [Fact]
    public async Task GenerateLocationReportsAsync_NoPreviousPeriodOrLocation_UsesUnknownNameAndZeroDeltas()
    {
        SetupAggregates([]);
        _locations.Setup(s => s.GetLocationByIdAsync("location-1")).ReturnsAsync((Location?)null);

        var result = await CreateService().GenerateLocationReportsAsync(
            [Aggregate("waiter-1", "location-1", 1, 1, 3, 2, 12, 1, 2)],
            Start, End, "user-1", new Mock<ILambdaLogger>().Object);

        var report = Assert.Single(result);
        Assert.Equal("Unknown", report.LocationName);
        Assert.Equal(0, report.DeltaRevenue);
        Assert.Equal(0, report.DeltaAvgCuisineFeedback);
    }

    [Fact]
    public async Task GenerateLocationReportsAsync_PreviousZeroRevenueAndRating_UsesZeroDeltas()
    {
        SetupAggregates([Aggregate("waiter-1", "location-1", 0, 1, 0, 1, 0, 1, 0)]);
        _locations.Setup(s => s.GetLocationByIdAsync("location-1")).ReturnsAsync(NewLocation("Central"));

        var result = await CreateService().GenerateLocationReportsAsync(
            [Aggregate("waiter-1", "location-1", 1, 1, 3, 2, 12, 1, 2)],
            Start, End, "user-1", new Mock<ILambdaLogger>().Object);

        var report = Assert.Single(result);
        Assert.Equal(0, report.DeltaRevenue);
        Assert.Equal(0, report.DeltaAvgCuisineFeedback);
    }

    [Fact]
    public async Task SaveReportsHistoryAsync_SavesEachReportOnce()
    {
        var reports = new List<ReportHistoryLocation> { LocationHistory("l1"), LocationHistory("l2") };
        _repository.Setup(r => r.SaveReportHistoryAsync(It.IsAny<ReportHistoryLocation>())).Returns(Task.CompletedTask);

        await CreateService().SaveReportsHistoryAsync(reports);

        foreach (var report in reports)
            _repository.Verify(r => r.SaveReportHistoryAsync(report), Times.Once);
    }

    [Fact]
    public async Task GetUserReportsListAsync_CombinesDistinctPeriodsAndUsesUnknownForDeletedLocation()
    {
        var waiter = WaiterHistory("location-1");
        var location = LocationHistory("location-2");
        _repository.Setup(r => r.GetWaiterReportsByUserIdAsync("user-1")).ReturnsAsync([waiter]);
        _repository.Setup(r => r.GetLocationReportsByUserIdAsync("user-1")).ReturnsAsync([location]);
        _locations.Setup(s => s.GetLocationByIdAsync("location-1")).ReturnsAsync(NewLocation("Central"));
        _locations.Setup(s => s.GetLocationByIdAsync("location-2")).ReturnsAsync((Location?)null);

        var result = await CreateService().GetUserReportsListAsync("user-1");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.LocationId == "location-1" && r.LocationName == "Central" && r.ReportType == ReportType.Service);
        Assert.Contains(result, r => r.LocationId == "location-2" && r.LocationName == "Unknown" && r.ReportType == ReportType.Sales);
    }

    [Theory]
    [InlineData(ReportType.Service)]
    [InlineData(ReportType.Sales)]
    public async Task QueryReportsHistoryAsync_FiltersByTypeLocationAndEnclosingDateRange(ReportType type)
    {
        var waiter = WaiterHistory("location-1");
        var location = LocationHistory("location-1");
        _repository.Setup(r => r.GetWaiterReportsByUserIdAsync("user-1")).ReturnsAsync(
            [waiter, WaiterHistory("other-location"), WaiterHistory("location-1", Start.AddDays(-1), End)]);
        _repository.Setup(r => r.GetLocationReportsByUserIdAsync("user-1")).ReturnsAsync(
            [location, LocationHistory("other-location"), LocationHistory("location-1", Start, End.AddDays(1))]);

        var result = await CreateService().QueryReportsHistoryAsync("user-1", type, "location-1", Start, End);

        Assert.Single(result!);
        Assert.Equal("location-1", result![0].LocationId);
        Assert.Equal(type, result[0].ReportType);
    }

    [Fact]
    public async Task QueryReportsHistoryAsync_UnsupportedType_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().QueryReportsHistoryAsync("user-1", (ReportType)99, "location-1", Start, End));
    }

    private void SetupAggregates(List<DailyAggregate> previous)
    {
        var previousStart = Start.AddDays(-7);
        var previousEnd = Start.AddDays(-1);
        _repository.Setup(r => r.GetAggregatesForPeriodAsync(
                previousStart.ToDateTime(TimeOnly.MinValue), previousEnd.ToDateTime(TimeOnly.MaxValue), "location-1"))
            .ReturnsAsync(previous);
    }

    private static Reservation ReservationWithStatus(ReservationStatus status) => new()
    {
        Id = "reservation-1",
        TableId = "table-1",
        WaiterId = "waiter-1",
        Status = status
    };

    private static Report NewReport() => new()
    {
        ReservationId = "reservation-1",
        WaiterId = "waiter-1",
        LocationId = "location-1",
        ReservationStart = Start.ToDateTime(TimeOnly.MinValue),
        ReservationEnd = End.ToDateTime(TimeOnly.MaxValue)
    };

    private static FeedbackResponseItem Feedback(string reservationId, FeedbackType type, int rating) => new()
    {
        ReservationId = reservationId,
        LocationId = "location-1",
        Type = type,
        Rating = rating
    };

    private static Location NewLocation(string name) => new()
    {
        Id = "location-1",
        Address = name,
        Description = "Restaurant",
        AverageOccupancy = "medium"
    };

    private static DailyAggregate Aggregate(
        string waiterId, string locationId, double service, int minimumService,
        double cuisine, int minimumCuisine, decimal revenue, double hours, int orders) => new()
    {
        WaiterId = waiterId,
        LocationId = locationId,
        AverageServiceRating = service,
        MinimumServiceRating = minimumService,
        AverageCuisineRating = cuisine,
        MinimumCuisineRating = minimumCuisine,
        TotalRevenue = revenue,
        HoursWorked = hours,
        OrdersProcessed = orders,
        Date = Start.ToDateTime(TimeOnly.MinValue)
    };

    private static ReportHistoryWaiter WaiterHistory(
        string locationId, DateOnly? start = null, DateOnly? end = null) => new()
    {
        Id = $"waiter-{locationId}-{start ?? Start}",
        UserId = "user-1",
        LocationId = locationId,
        LocationName = "Restaurant",
        WaiterId = "waiter-1",
        WaiterName = "Jordan",
        WaiterEmail = "jordan@example.com",
        ReportType = ReportType.Service,
        StartDate = start ?? Start,
        EndDate = end ?? End
    };

    private static ReportHistoryLocation LocationHistory(
        string locationId, DateOnly? start = null, DateOnly? end = null) => new()
    {
        Id = $"location-{locationId}-{start ?? Start}",
        UserId = "user-1",
        LocationId = locationId,
        LocationName = "Restaurant",
        ReportType = ReportType.Sales,
        StartDate = start ?? Start,
        EndDate = end ?? End
    };
}
