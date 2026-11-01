using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants.Reports;
using APIHandler.Entities;
using APIHandler.Entities.Reports;
using APIHandler.Handlers.Reports;
using APIHandler.Models.Reports;
using APIHandler.Services.Locations;
using APIHandler.Services.Reports.Interfaces;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Reports;

public class ReportHandlersTests
{
    private readonly Mock<IReportService> _reports = new();
    private readonly Mock<ILocationService> _locations = new();
    private readonly Mock<IReportFileService> _files = new();
    private readonly Mock<ILambdaLogger> _logger = new();
    private readonly Mock<ILambdaContext> _context = new();

    public ReportHandlersTests() => _context.SetupGet(c => c.Logger).Returns(_logger.Object);

    [Theory]
    [InlineData("Service", "2026-05-01", "2026-05-07", "", 400)]
    [InlineData("Service", "invalid", "2026-05-07", "loc-1", 400)]
    [InlineData("Service", "2026-05-01", "invalid", "loc-1", 400)]
    [InlineData("Service", "2026-05-08", "2026-05-07", "loc-1", 400)]
    [InlineData("Other", "2026-05-01", "2026-05-07", "loc-1", 400)]
    public async Task GenerateReportHandler_InvalidQuery_ReturnsBadRequest(
        string? type, string start, string end, string locationId, int statusCode)
    {
        var request = GenerateRequest(type, start, end, locationId, "user-1");

        var response = await new GenerateReportHandler(_reports.Object, _locations.Object)
            .HandleRequest(request, _context.Object);

        Assert.Equal(statusCode, response.StatusCode);
        _locations.VerifyNoOtherCalls();
        _reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateReportHandler_LocationDoesNotExist_ReturnsNotFound()
    {
        _locations.Setup(s => s.GetLocationByIdAsync("loc-1")).ReturnsAsync((APIHandler.Entities.Location?)null);

        var response = await new GenerateReportHandler(_reports.Object, _locations.Object)
            .HandleRequest(GenerateRequest("Service", "2026-05-01", "2026-05-07", "loc-1", "user-1"), _context.Object);

        Assert.Equal(404, response.StatusCode);
        _reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateReportHandler_UserNotAuthenticated_ReturnsUnauthorizedAfterLocationCheck()
    {
        LocationExists();

        var response = await new GenerateReportHandler(_reports.Object, _locations.Object)
            .HandleRequest(GenerateRequest("Service", "2026-05-01", "2026-05-07", "loc-1", null), _context.Object);

        Assert.Equal(401, response.StatusCode);
        _reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateReportHandler_ExistingServiceHistory_ExposesReportSerializationFailure()
    {
        LocationExists();
        var report = WaiterReport();
        _reports.Setup(s => s.QueryReportsHistoryAsync("user-1", ReportType.Service, "loc-1",
                new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 7)))
            .ReturnsAsync([report]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GenerateReportHandler(_reports.Object, _locations.Object)
                .HandleRequest(GenerateRequest("Service", "2026-05-01", "2026-05-07", "loc-1", "user-1"), _context.Object));

        Assert.Contains("required", exception.Message);
        _reports.Verify(s => s.GetAggregatesForPeriodAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GenerateReportHandler_EmptyHistory_ThrowsDuringHistoryLogSerializationBeforeAggregateLookup()
    {
        LocationExists();
        SetupNoCachedHistory();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GenerateReportHandler(_reports.Object, _locations.Object)
                .HandleRequest(GenerateRequest("Service", "2026-05-01", "2026-05-07", "loc-1", "user-1"), _context.Object));

        Assert.Contains("required", exception.Message);
        _reports.Verify(s => s.GetAggregatesForPeriodAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GenerateReportHandler_HistoryLogSerializesBaseTypeAndThrowsBeforeFallback()
    {
        LocationExists();
        _reports.Setup(s => s.QueryReportsHistoryAsync("user-1", ReportType.Service, "loc-1",
                new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 7)))
            .ReturnsAsync([LocationReport()]);
        _reports.Setup(s => s.GetAggregatesForPeriodAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), "loc-1"))
            .ReturnsAsync([]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GenerateReportHandler(_reports.Object, _locations.Object)
                .HandleRequest(GenerateRequest("Service", "2026-05-01", "2026-05-07", "loc-1", "user-1"), _context.Object));

        Assert.Contains("required", exception.Message);
        _reports.Verify(s => s.GetAggregatesForPeriodAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), "loc-1"), Times.Never);
    }

    [Fact]
    public async Task DownloadReportHandler_MissingLocation_ReturnsBadRequestWithoutServiceCall()
    {
        var response = await new DownloadReportHandler(_files.Object, _reports.Object)
            .HandleRequest(DownloadRequest(null, "csv", "Service", "2026-05-01", "2026-05-07", "user-1"), _context.Object);

        Assert.Equal(400, response.StatusCode);
        _reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DownloadReportHandler_InvalidRangeOrType_ReturnsBadRequest()
    {
        var handler = new DownloadReportHandler(_files.Object, _reports.Object);
        var reversed = await handler.HandleRequest(
            DownloadRequest("loc-1", "csv", "Service", "2026-05-08", "2026-05-07", "user-1"), _context.Object);
        var invalidType = await handler.HandleRequest(
            DownloadRequest("loc-1", "csv", "Other", "2026-05-01", "2026-05-07", "user-1"), _context.Object);

        Assert.Equal(400, reversed.StatusCode);
        Assert.Equal(400, invalidType.StatusCode);
        _reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DownloadReportHandler_UnauthenticatedUser_ReturnsUnauthorized()
    {
        var response = await new DownloadReportHandler(_files.Object, _reports.Object)
            .HandleRequest(DownloadRequest("loc-1", "csv", "Service", "2026-05-01", "2026-05-07", null), _context.Object);

        Assert.Equal(401, response.StatusCode);
        _reports.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("csv", "text/csv", false)]
    [InlineData("excel", "application/vnd.ms-excel", true)]
    [InlineData("pdf", "application/pdf", true)]
    [InlineData("unknown", "text/csv", false)]
    public async Task DownloadReportHandler_ValidRequest_UsesRequestedOrFallbackFileFormat(
        string format, string contentType, bool base64)
    {
        var histories = new List<ReportHistoryBase> { WaiterReport() };
        _reports.Setup(s => s.QueryReportsHistoryAsync("user-1", ReportType.Service, "loc-1",
                new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 7)))
            .ReturnsAsync(histories);
        var file = new ReportHistoryFileDto { FileContent = [1, 2, 3], FileName = "report.dat", ContentType = contentType };
        _files.Setup(s => s.GenerateCsvAsync(histories)).ReturnsAsync(file);
        _files.Setup(s => s.GenerateExcel(histories)).Returns(file);
        _files.Setup(s => s.GeneratePdf(histories)).Returns(file);

        var response = await new DownloadReportHandler(_files.Object, _reports.Object)
            .HandleRequest(DownloadRequest("loc-1", format, "Service", "2026-05-01", "2026-05-07", "user-1"), _context.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(base64, response.IsBase64Encoded);
        Assert.Equal(contentType, response.Headers["Content-Type"]);
        Assert.Equal("attachment; filename=\"report.dat\"", response.Headers["Content-Disposition"]);
        if (format == "excel")
            _files.Verify(s => s.GenerateExcel(histories), Times.Once);
        else if (format == "pdf")
            _files.Verify(s => s.GeneratePdf(histories), Times.Once);
        else
            _files.Verify(s => s.GenerateCsvAsync(histories), Times.Once);
    }

    [Fact]
    public async Task GetUserReportsHandler_MissingIdentity_ReturnsUnauthorized()
    {
        var response = await new GetUserReportsHandler(_reports.Object)
            .HandleRequest(new APIGatewayProxyRequest { RequestContext = new() }, _context.Object);

        Assert.Equal(401, response.StatusCode);
        _reports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetUserReportsHandler_AuthenticatedUser_ReturnsTheirReportHistory()
    {
        List<UserReportsHistoryDto> expected =
        [
            new() { UserId = "user-1", LocationId = "loc-1", LocationName = "Central" }
        ];
        _reports.Setup(s => s.GetUserReportsListAsync("user-1")).ReturnsAsync(expected);

        var response = await new GetUserReportsHandler(_reports.Object)
            .HandleRequest(RequestWithUser("user-1"), _context.Object);

        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal("user-1", body.RootElement[0].GetProperty("userId").GetString());
    }

    private void LocationExists() =>
        _locations.Setup(s => s.GetLocationByIdAsync("loc-1")).ReturnsAsync(new APIHandler.Entities.Location
        {
            Id = "loc-1", Address = "Central", Description = "Restaurant", AverageOccupancy = "medium"
        });

    private void SetupNoCachedHistory()
    {
        _reports.Setup(s => s.QueryReportsHistoryAsync("user-1", It.IsAny<ReportType>(), "loc-1",
                new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 7)))
            .ReturnsAsync([]);
    }

    private static APIGatewayProxyRequest GenerateRequest(string? type, string start, string end, string locationId, string? userId)
    {
        var query = new Dictionary<string, string>();
        if (type is not null) query["type"] = type;
        if (!string.IsNullOrEmpty(locationId)) query["locationId"] = locationId;
        if (start is not null) query["startDate"] = start;
        if (end is not null) query["endDate"] = end;
        return RequestWithUser(userId, query);
    }

    private static APIGatewayProxyRequest DownloadRequest(
        string? locationId, string? format, string? type, string? start, string? end, string? userId)
    {
        var query = new Dictionary<string, string>();
        if (locationId is not null) query["locationId"] = locationId;
        if (format is not null) query["format"] = format;
        if (type is not null) query["type"] = type;
        if (start is not null) query["startDate"] = start;
        if (end is not null) query["endDate"] = end;
        return RequestWithUser(userId, query);
    }

    private static APIGatewayProxyRequest RequestWithUser(string? userId, Dictionary<string, string>? query = null) =>
        new()
        {
            QueryStringParameters = query,
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = userId is null
                    ? null
                    : new APIGatewayCustomAuthorizerContext
                    {
                        Claims = new Dictionary<string, string> { ["sub"] = userId }
                    }
            }
        };

    private static DailyAggregate Aggregate() => new()
    {
        WaiterId = "waiter-1", LocationId = "loc-1", Date = new DateTime(2026, 5, 2),
        AverageServiceRating = 4, AverageCuisineRating = 4, MinimumServiceRating = 3,
        MinimumCuisineRating = 3, OrdersProcessed = 2, HoursWorked = 2
    };

    private static ReportHistoryWaiter WaiterReport() => new()
    {
        Id = "waiter-report", UserId = "user-1", LocationId = "loc-1", LocationName = "Central",
        WaiterId = "waiter-1", WaiterName = "Jordan", WaiterEmail = "jordan@example.com",
        StartDate = new DateOnly(2026, 5, 1), EndDate = new DateOnly(2026, 5, 7), ReportType = ReportType.Service
    };

    private static ReportHistoryLocation LocationReport() => new()
    {
        Id = "location-report", UserId = "user-1", LocationId = "loc-1", LocationName = "Central",
        StartDate = new DateOnly(2026, 5, 1), EndDate = new DateOnly(2026, 5, 7), ReportType = ReportType.Sales
    };
}
