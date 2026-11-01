using APIHandler.Entities.Reports;
using APIHandler.Services.Reports;
using NPOI.HSSF.UserModel;
using System.Text;

namespace APIHandler.Tests.Services;

public class ReportFileServiceTests
{
    private readonly ReportFileService _service = new();

    [Fact]
    public async Task GenerateCsvAsync_WaiterReports_FormatsWaiterColumnsAndValues()
    {
        var report = WaiterReport();

        var result = await _service.GenerateCsvAsync([report]);
        var content = Encoding.UTF8.GetString(result.FileContent);

        Assert.Equal("text/csv", result.ContentType);
        Assert.Equal("report_2026-03-01-2026-03-07.csv", result.FileName);
        Assert.Contains("Location,Waiter Name,Email,Total Orders,Working Hours,Average Feedback,Minimal Feedback,Delta Avg Feedback,Delta Orders", content);
        Assert.Contains("Central,Jordan Lee,jordan@example.com,8,12.35,4.26,3.50,12.346%,25%", content);
    }

    [Fact]
    public async Task GenerateCsvAsync_LocationReports_FormatsLocationColumnsAndValues()
    {
        var report = LocationReport();

        var result = await _service.GenerateCsvAsync([report]);
        var content = Encoding.UTF8.GetString(result.FileContent);

        Assert.Equal("text/csv", result.ContentType);
        Assert.Equal("report_2026-03-01-2026-03-07.csv", result.FileName);
        Assert.Contains("Location,Total Orders,Revenue,Average Cuisine Feedback,Minimal Cuisine Feedback,Delta Avg Cuisine Feedback,Delta Revenue", content);
        Assert.Contains("Central,14,123.46,4.13,3.25,8.765%,10.125%", content);
    }

    [Fact]
    public async Task GenerateCsvAsync_OtherReportType_ProducesEmptyContentButKeepsDateRangeName()
    {
        var report = BaseReport();

        var result = await _service.GenerateCsvAsync([report]);

        Assert.Empty(result.FileContent);
        Assert.Equal("text/csv", result.ContentType);
        Assert.Equal("report_2026-03-01-2026-03-07.csv", result.FileName);
    }

    [Fact]
    public void GenerateExcel_WaiterReports_CreatesWorkbookWithFormattedWaiterData()
    {
        var result = _service.GenerateExcel([WaiterReport()]);

        Assert.Equal("application/vnd.ms-excel", result.ContentType);
        Assert.Equal("report_2026-03-01-2026-03-07.xls", result.FileName);
        using var stream = new MemoryStream(result.FileContent);
        var sheet = new HSSFWorkbook(stream).GetSheet("Report");

        Assert.Equal("Waiter ID", sheet.GetRow(0).GetCell(0).StringCellValue);
        Assert.Equal("Name", sheet.GetRow(0).GetCell(1).StringCellValue);
        Assert.Equal("waiter-1", sheet.GetRow(1).GetCell(0).StringCellValue);
        Assert.Equal("Jordan Lee", sheet.GetRow(1).GetCell(1).StringCellValue);
        Assert.Equal("jordan@example.com", sheet.GetRow(1).GetCell(2).StringCellValue);
        Assert.Equal(12.345, sheet.GetRow(1).GetCell(3).NumericCellValue, 3);
        Assert.Equal(8, sheet.GetRow(1).GetCell(4).NumericCellValue);
        Assert.Equal(4.26, sheet.GetRow(1).GetCell(5).NumericCellValue, 2);
        Assert.Equal("12.35%", sheet.GetRow(1).GetCell(7).StringCellValue);
        Assert.Equal("25%", sheet.GetRow(1).GetCell(8).StringCellValue);
    }

    [Fact]
    public void GenerateExcel_LocationReports_CreatesWorkbookWithLocationData()
    {
        var result = _service.GenerateExcel([LocationReport()]);

        using var stream = new MemoryStream(result.FileContent);
        var sheet = new HSSFWorkbook(stream).GetSheet("Report");

        Assert.Equal("Location Name", sheet.GetRow(0).GetCell(0).StringCellValue);
        Assert.Equal("Central", sheet.GetRow(1).GetCell(0).StringCellValue);
        Assert.Equal(14, sheet.GetRow(1).GetCell(1).NumericCellValue);
        Assert.Equal(123.456, sheet.GetRow(1).GetCell(2).NumericCellValue, 3);
        Assert.Equal(4.13, sheet.GetRow(1).GetCell(3).NumericCellValue, 2);
        Assert.Equal("8.77%", sheet.GetRow(1).GetCell(5).StringCellValue);
        Assert.Equal("10.125%", sheet.GetRow(1).GetCell(6).StringCellValue);
    }

    [Fact]
    public void GenerateExcel_OtherReportType_CreatesWorkbookWithHeaderOnly()
    {
        var result = _service.GenerateExcel([BaseReport()]);

        using var stream = new MemoryStream(result.FileContent);
        var sheet = new HSSFWorkbook(stream).GetSheet("Report");
        Assert.Equal(0, sheet.LastRowNum);
        Assert.Null(sheet.GetRow(0));
    }

    [Theory]
    [MemberData(nameof(PdfReportCases))]
    public void GeneratePdf_SupportedAndFallbackReportTypes_ReturnsValidPdf(ReportHistoryBase report)
    {
        var result = _service.GeneratePdf([report]);

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("report_2026-03-01-2026-03-07.pdf", result.FileName);
        Assert.True(result.FileContent.Length > 1_000);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(result.FileContent, 0, 4));
    }

    public static IEnumerable<object[]> PdfReportCases()
    {
        yield return [WaiterReport()];
        yield return [LocationReport()];
        yield return [BaseReport()];
    }

    private static ReportHistoryWaiter WaiterReport() => new()
    {
        Id = "waiter-report",
        UserId = "user-1",
        LocationId = "location-1",
        LocationName = "Central",
        WaiterId = "waiter-1",
        WaiterName = "Jordan Lee",
        WaiterEmail = "jordan@example.com",
        StartDate = new DateOnly(2026, 3, 1),
        EndDate = new DateOnly(2026, 3, 7),
        WorkingHours = 12.345,
        TotalOrders = 8,
        AvgServiceFeedback = 4.256,
        MinServiceFeedback = 3.5,
        DeltaAvgServiceFeedback = 12.3456,
        DeltaTotalOrders = 25
    };

    private static ReportHistoryLocation LocationReport() => new()
    {
        Id = "location-report",
        UserId = "user-1",
        LocationId = "location-1",
        LocationName = "Central",
        StartDate = new DateOnly(2026, 3, 1),
        EndDate = new DateOnly(2026, 3, 7),
        TotalOrders = 14,
        Revenue = 123.456m,
        AvgCuisineFeedback = 4.126,
        MinCuisineFeedback = 3.25,
        DeltaAvgCuisineFeedback = 8.7654,
        DeltaRevenue = 10.1254m
    };

    private static ReportHistoryBase BaseReport() => new()
    {
        Id = "base-report",
        UserId = "user-1",
        LocationId = "location-1",
        LocationName = "Central",
        StartDate = new DateOnly(2026, 3, 1),
        EndDate = new DateOnly(2026, 3, 7)
    };
}
