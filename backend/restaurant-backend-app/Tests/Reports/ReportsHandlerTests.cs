using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Moq;
using ReportsHandler.Entities;
using ReportsHandler.Models;
using ReportsHandler.Services;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace APIHandler.Tests.Reports;

public class ReportsHandlerTests
{
    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"reservationId":"r1","waiterId":"w1","locationId":"l1","reservationStart":"2026-07-01T10:00:00","reservationEnd":"2026-07-01T09:00:00","serviceRating":5,"cuisineRating":5,"revenue":10}""")]
    [InlineData("""{"reservationId":"r1","waiterId":"w1","locationId":"l1","reservationStart":"2026-07-01T10:00:00","reservationEnd":"2026-07-01T11:00:00","serviceRating":6,"cuisineRating":5,"revenue":10}""")]
    public async Task FunctionHandler_MalformedOrInvalidReport_AcknowledgesMessageWithoutServiceCall(string body)
    {
        var service = new Mock<IReportService>();
        var logger = new Mock<ILambdaLogger>();
        var context = CreateContext(logger);

        var result = await CreateFunction(service).FunctionHandler(
            Event(("message-1", body)), context.Object);

        Assert.Empty(result.BatchItemFailures);
        service.VerifyNoOtherCalls();
        logger.Verify(log => log.LogError(It.Is<string>(message => message.Contains("PERMANENT_ERROR"))), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_ValidReport_UpdatesAggregateAndAcknowledgesMessage()
    {
        var service = new Mock<IReportService>();
        var logger = new Mock<ILambdaLogger>();
        var report = ValidReport();
        var aggregate = NewAggregate();
        service.Setup(s => s.GetOrCreateDailyAggregateAsync(report.WaiterId, report.ReservationStart.Date))
            .ReturnsAsync(aggregate);
        service.Setup(s => s.UpdateDailyAggregateAsync(aggregate)).Returns(Task.CompletedTask);

        var result = await CreateFunction(service).FunctionHandler(
            Event(("message-2", JsonSerializer.Serialize(report))), CreateContext(logger).Object);

        Assert.Empty(result.BatchItemFailures);
        Assert.Equal(5d, aggregate.AverageServiceRating);
        Assert.Equal(4d, aggregate.AverageCuisineRating);
        Assert.Equal(5, aggregate.MinimumServiceRating);
        Assert.Equal(4, aggregate.MinimumCuisineRating);
        Assert.Equal(25m, aggregate.TotalRevenue);
        Assert.Equal(1.5, aggregate.HoursWorked);
        Assert.Equal(1, aggregate.OrdersProcessed);
        Assert.Equal("location-1", aggregate.LocationId);
        service.Verify(s => s.GetOrCreateDailyAggregateAsync(report.WaiterId, report.ReservationStart.Date), Times.Once);
        service.Verify(s => s.UpdateDailyAggregateAsync(aggregate), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_AggregateLoadFailure_ReportsBatchItemFailure()
    {
        var service = new Mock<IReportService>();
        service.Setup(s => s.GetOrCreateDailyAggregateAsync("waiter-1", new DateTime(2026, 7, 1)))
            .ThrowsAsync(new InvalidOperationException("temporary read failure"));

        var result = await CreateFunction(service).FunctionHandler(
            Event(("retry-me", JsonSerializer.Serialize(ValidReport()))),
            CreateContext(new Mock<ILambdaLogger>()).Object);

        Assert.Equal("retry-me", Assert.Single(result.BatchItemFailures).ItemIdentifier);
        service.Verify(s => s.UpdateDailyAggregateAsync(It.IsAny<DailyAggregate>()), Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_AggregateSaveFailure_ReportsBatchItemFailure()
    {
        var service = new Mock<IReportService>();
        var report = ValidReport();
        var aggregate = NewAggregate();
        service.Setup(s => s.GetOrCreateDailyAggregateAsync(report.WaiterId, report.ReservationStart.Date))
            .ReturnsAsync(aggregate);
        service.Setup(s => s.UpdateDailyAggregateAsync(aggregate))
            .ThrowsAsync(new Exception("temporary write failure"));

        var result = await CreateFunction(service).FunctionHandler(
            Event(("retry-save", JsonSerializer.Serialize(report))),
            CreateContext(new Mock<ILambdaLogger>()).Object);

        Assert.Equal("retry-save", Assert.Single(result.BatchItemFailures).ItemIdentifier);
        service.Verify(s => s.UpdateDailyAggregateAsync(aggregate), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_InconsistentAggregate_AcknowledgesPermanentDataError()
    {
        var service = new Mock<IReportService>();
        var report = ValidReport();
        var aggregate = NewAggregate();
        aggregate.LocationId = "different-location";
        service.Setup(s => s.GetOrCreateDailyAggregateAsync(report.WaiterId, report.ReservationStart.Date))
            .ReturnsAsync(aggregate);

        var result = await CreateFunction(service).FunctionHandler(
            Event(("permanent-error", JsonSerializer.Serialize(report))),
            CreateContext(new Mock<ILambdaLogger>()).Object);

        Assert.Empty(result.BatchItemFailures);
        service.Verify(s => s.UpdateDailyAggregateAsync(It.IsAny<DailyAggregate>()), Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_Batch_ContinuesAfterTransientFailureAndPreservesOnlyFailedMessage()
    {
        var service = new Mock<IReportService>();
        var first = ValidReport();
        first.WaiterId = "waiter-fails";
        var second = ValidReport();
        service.Setup(s => s.GetOrCreateDailyAggregateAsync("waiter-fails", first.ReservationStart.Date))
            .ThrowsAsync(new Exception("temporary failure"));
        service.Setup(s => s.GetOrCreateDailyAggregateAsync(second.WaiterId, second.ReservationStart.Date))
            .ReturnsAsync(NewAggregate());
        service.Setup(s => s.UpdateDailyAggregateAsync(It.IsAny<DailyAggregate>()))
            .Returns(Task.CompletedTask);

        var result = await CreateFunction(service).FunctionHandler(
            Event(
                ("failed", JsonSerializer.Serialize(first)),
                ("succeeded", JsonSerializer.Serialize(second))),
            CreateContext(new Mock<ILambdaLogger>()).Object);

        Assert.Equal("failed", Assert.Single(result.BatchItemFailures).ItemIdentifier);
        service.Verify(s => s.GetOrCreateDailyAggregateAsync("waiter-1", second.ReservationStart.Date), Times.Once);
        service.Verify(s => s.UpdateDailyAggregateAsync(It.IsAny<DailyAggregate>()), Times.Once);
    }

    private static ReportsHandler.Function CreateFunction(Mock<IReportService> service)
    {
        // Avoid the Function field initializer, which creates a concrete DynamoDB-backed service.
        // Replacing that dependency keeps the public batch flow under test without AWS access.
        var function = (ReportsHandler.Function)RuntimeHelpers.GetUninitializedObject(typeof(ReportsHandler.Function));
        typeof(ReportsHandler.Function)
            .GetField("_reportService", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(function, service.Object);
        return function;
    }

    private static Mock<ILambdaContext> CreateContext(Mock<ILambdaLogger> logger)
    {
        var context = new Mock<ILambdaContext>();
        context.SetupGet(c => c.Logger).Returns(logger.Object);
        return context;
    }

    private static SQSEvent Event(params (string Id, string Body)[] messages)
        => new()
        {
            Records = messages.Select(message => new SQSEvent.SQSMessage
            {
                MessageId = message.Id,
                Body = message.Body
            }).ToList()
        };

    private static Report ValidReport() => new()
    {
        ReservationId = "reservation-1",
        CreatedAt = new DateTime(2026, 7, 1, 9, 0, 0),
        ReservationStart = new DateTime(2026, 7, 1, 10, 0, 0),
        ReservationEnd = new DateTime(2026, 7, 1, 11, 30, 0),
        WaiterId = "waiter-1",
        LocationId = "location-1",
        ServiceRating = 5,
        CuisineRating = 4,
        Revenue = 25
    };

    private static DailyAggregate NewAggregate() => new()
    {
        WaiterId = "waiter-1",
        LocationId = "",
        Date = new DateTime(2026, 7, 1),
        AverageServiceRating = 0,
        MinimumServiceRating = int.MaxValue,
        AverageCuisineRating = 0,
        MinimumCuisineRating = int.MaxValue,
        TotalRevenue = 0,
        HoursWorked = 0,
        OrdersProcessed = 0
    };
}
