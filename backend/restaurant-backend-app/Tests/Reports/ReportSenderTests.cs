using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.Core;
using Moq;
using ReportSender;
using ReportSender.Entities;
using ReportSender.Entities.Reports;
using ReportSender.Models.Reports;
using ReportSender.Services;
using ReportSender.Services.Reports;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace APIHandler.Tests.Reports;

public class ReportSenderTests
{
    [Fact]
    public void Mapper_ToLocationReportsAsync_CombinesCurrentAndPreviousPeriodTotals()
    {
        var current = new List<DailyAggregate>
        {
            Aggregate(revenue: 150m, cuisine: 4, minimumCuisine: 2, orders: 3),
            Aggregate(revenue: 50m, cuisine: 5, minimumCuisine: 3, orders: 2)
        };
        var previous = new List<DailyAggregate>
        {
            Aggregate(revenue: 100m, cuisine: 4, minimumCuisine: 1, orders: 1)
        };

        var report = current.ToLocationReportsAsync(
            previous, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7), "Central");

        Assert.Equal("location-1", report.LocationId);
        Assert.Equal("Central", report.LocationName);
        Assert.Equal(5, report.TotalOrders);
        Assert.Equal(4.5, report.AvgCuisineFeedback);
        Assert.Equal(2, report.MinCuisineFeedback);
        Assert.Equal(200, report.Revenue);
        Assert.Equal(100, report.DeltaRevenue);
        Assert.Equal(12.5, report.DeltaAvgCuisineFeedback);
        Assert.Equal(new DateOnly(2026, 1, 1), report.StartDate);
        Assert.Equal(new DateOnly(2026, 1, 7), report.EndDate);
        Assert.Equal(ReportType.Sales, report.ReportType);
    }

    [Fact]
    public void Mapper_ToLocationReportsAsync_NoPreviousPeriod_UsesZeroDeltas()
    {
        var report = new List<DailyAggregate> { Aggregate(revenue: 80m, cuisine: 3.5, minimumCuisine: 3, orders: 2) }
            .ToLocationReportsAsync([], new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 7), "North");

        Assert.Equal(0, report.DeltaRevenue);
        Assert.Equal(0, report.DeltaAvgCuisineFeedback);
        Assert.Equal(80, report.Revenue);
    }

    [Fact]
    public void Mapper_ToWaiterReportsAsync_MapsAggregateStatisticsAndPeriodDeltas()
    {
        var current = new List<DailyAggregate>
        {
            Aggregate(service: 4, minimumService: 3, orders: 6, hours: 10),
            Aggregate(service: 5, minimumService: 4, orders: 4, hours: 8)
        };
        var previous = new List<DailyAggregate>
        {
            Aggregate(service: 4, minimumService: 2, orders: 5, hours: 6)
        };
        var user = new UserType
        {
            Attributes =
            [
                new AttributeType { Name = "given_name", Value = "Alex" },
                new AttributeType { Name = "family_name", Value = "Smith" },
                new AttributeType { Name = "email", Value = "alex@example.com" }
            ]
        };

        var report = current.ToWaiterReportsAsync(
            previous, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 7), user);

        Assert.Equal("waiter-1", report.WaiterId);
        Assert.Equal("alex@example.com", report.WaiterEmail);
        Assert.Equal(18, report.WorkingHours);
        Assert.Equal(10, report.TotalOrders);
        Assert.Equal(4.5, report.AvgServiceFeedback);
        Assert.Equal(3, report.MinServiceFeedback);
        Assert.Equal(12.5, report.DeltaAvgServiceFeedback);
        Assert.Equal(100, report.DeltaTotalOrders);
        Assert.Equal(ReportType.Service, report.ReportType);
    }

    [Fact]
    public void Mapper_ToWaiterReportsAsync_WithoutPreviousOrUser_UsesDefaults()
    {
        var report = new List<DailyAggregate> { Aggregate(service: 3, minimumService: 2, orders: 1, hours: 2) }
            .ToWaiterReportsAsync([], new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 7), new UserType { Attributes = [] });

        Assert.Equal(0, report.DeltaAvgServiceFeedback);
        Assert.Equal(0, report.DeltaTotalOrders);
        Assert.StartsWith("Unknown", report.WaiterName);
        Assert.Equal("Unknown", report.WaiterEmail);
    }

    [Fact]
    public void ReportFileService_GenerateCsvAsync_LocationReports_WritesHeaderRowsAndDateRangeName()
    {
        var reports = new ReportHistoryBase[]
        {
            new ReportHistoryLocation
            {
                Id = "report-1",
                LocationId = "loc-1",
                LocationName = "Central",
                StartDate = new DateOnly(2026, 5, 1),
                EndDate = new DateOnly(2026, 5, 7),
                TotalOrders = 5,
                Revenue = 125.5,
                AvgCuisineFeedback = 4.2,
                MinCuisineFeedback = 3,
                DeltaAvgCuisineFeedback = 2,
                DeltaRevenue = 10
            }
        };

        var result = new ReportFileService().GenerateCsvAsync(reports);
        var csv = Encoding.UTF8.GetString(result.FileContent);

        Assert.Equal("text/csv", result.ContentType);
        Assert.Equal("report_2026-05-01-2026-05-07.csv", result.FileName);
        Assert.Contains("Location,TotalOrders,Revenue,AvgCuisineFeedback", csv);
        Assert.Contains("Central,5,125.5,4.2,3,2,10", csv);
    }

    [Fact]
    public void ReportFileService_GenerateCsvAsync_WaiterReports_WritesWaiterColumns()
    {
        var reports = new ReportHistoryBase[]
        {
            new ReportHistoryWaiter
            {
                Id = "report-2",
                LocationName = "East",
                WaiterName = "Jamie Lee",
                WaiterEmail = "jamie@example.com",
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 7),
                TotalOrders = 8,
                WorkingHours = 12.5,
                AvgServiceFeedback = 4.5,
                MinServiceFeedback = 3,
                DeltaAvgServiceFeedback = 5,
                DeltaTotalOrders = 20
            }
        };

        var result = new ReportFileService().GenerateCsvAsync(reports);
        var csv = Encoding.UTF8.GetString(result.FileContent);

        Assert.Contains("Location,Waiter,Email,TotalOrders,WorkingHours", csv);
        Assert.Contains("East,Jamie Lee,jamie@example.com,8,12.5,4.5,3,5,20", csv);
        Assert.Equal("report_2026-06-01-2026-06-07.csv", result.FileName);
    }

    [Fact]
    public void ReportFileService_GenerateCsvAsync_EmptyReports_ReturnsEmptyCsvWithDefaultName()
    {
        var result = new ReportFileService().GenerateCsvAsync([]);

        Assert.Empty(result.FileContent);
        Assert.Equal("report.csv", result.FileName);
        Assert.Equal("text/csv", result.ContentType);
    }

    [Fact]
    public async Task FunctionHandler_GeneratesBothReportsAndSendsFilesOnlyToAdminEmails()
    {
        var reportService = new Mock<IReportService>();
        var fileService = new Mock<IReportFileService>();
        var userService = new Mock<IUserService>();
        var emailService = new Mock<IEmailService>();
        var locations = new List<ReportHistoryLocation>
        {
            new() { Id = "location-report", LocationId = "loc-1", LocationName = "Central" }
        };
        var waiters = new List<ReportHistoryWaiter>
        {
            new() { Id = "waiter-report", WaiterId = "waiter-1", WaiterName = "Alex", WaiterEmail = "alex@example.com" }
        };
        reportService.Setup(s => s.GetLocationsReportAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<ILambdaLogger>()))
            .ReturnsAsync(locations);
        reportService.Setup(s => s.GetWaitersReportAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<ILambdaLogger>()))
            .ReturnsAsync(waiters);
        var locationFile = new ReportHistoryFileDto { FileContent = [1], ContentType = "text/csv", FileName = "locations.csv" };
        var waiterFile = new ReportHistoryFileDto { FileContent = [2], ContentType = "text/csv", FileName = "waiters.csv" };
        fileService.SetupSequence(s => s.GenerateCsvAsync(It.IsAny<IEnumerable<ReportHistoryBase>>()))
            .Returns(locationFile)
            .Returns(waiterFile);
        userService.Setup(s => s.GetUsersByGroupAsync(Roles.Admin.ToString()))
            .ReturnsAsync(
            [
                new UserType { Attributes = [new AttributeType { Name = "email", Value = "admin@example.com" }] },
                new UserType { Attributes = [new AttributeType { Name = "email", Value = "" }] },
                new UserType { Attributes = [new AttributeType { Name = "given_name", Value = "No Email" }] }
            ]);
        emailService.Setup(s => s.SendEmailsWithAttachmentsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<ReportHistoryFileDto>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var function = (ReportSender.Function)RuntimeHelpers.GetUninitializedObject(typeof(ReportSender.Function));
        SetFunctionField(function, "service", reportService.Object);
        SetFunctionField(function, "reportFileService", fileService.Object);
        SetFunctionField(function, "userService", userService.Object);
        SetFunctionField(function, "emailService", emailService.Object);
        var logger = new Mock<ILambdaLogger>();
        var context = new Mock<ILambdaContext>();
        context.SetupGet(c => c.Logger).Returns(logger.Object);

        await function.FunctionHandler(context.Object);

        reportService.Verify(s => s.GetLocationsReportAsync(
            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), logger.Object), Times.Once);
        reportService.Verify(s => s.GetWaitersReportAsync(
            It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), logger.Object), Times.Once);
        fileService.Verify(s => s.GenerateCsvAsync(locations), Times.Once);
        fileService.Verify(s => s.GenerateCsvAsync(waiters), Times.Once);
        userService.Verify(s => s.GetUsersByGroupAsync(Roles.Admin.ToString()), Times.Once);
        emailService.Verify(s => s.SendEmailsWithAttachmentsAsync(
            It.Is<IEnumerable<string>>(emails => emails.SequenceEqual(new[] { "admin@example.com" })),
            It.Is<IEnumerable<ReportHistoryFileDto>>(files => files.SequenceEqual(new[] { locationFile, waiterFile })),
            "Weekly Green & Tasty Report",
            "Please find the attached report."), Times.Once);
    }

    private static DailyAggregate Aggregate(
        decimal revenue = 0,
        double cuisine = 0,
        int minimumCuisine = 1,
        double service = 0,
        int minimumService = 1,
        int orders = 0,
        double hours = 0)
        => new()
        {
            WaiterId = "waiter-1",
            LocationId = "location-1",
            Date = new DateTime(2026, 1, 2),
            TotalRevenue = revenue,
            AverageCuisineRating = cuisine,
            MinimumCuisineRating = minimumCuisine,
            AverageServiceRating = service,
            MinimumServiceRating = minimumService,
            OrdersProcessed = orders,
            HoursWorked = hours
        };

    private static void SetFunctionField(ReportSender.Function function, string field, object value)
        => typeof(ReportSender.Function)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(function, value);
}
