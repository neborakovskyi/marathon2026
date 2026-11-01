using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Handlers.TimeSlots;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.TimeSlots;

[Collection("ResourceNamesEnvironment")]
public class GetAvailableTimeSlotsHandlerTests
{
    private readonly Mock<IDynamoDBContext> _db = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public GetAvailableTimeSlotsHandlerTests()
    {
        _context.SetupGet(c => c.Logger).Returns(_logger.Object);
        Environment.SetEnvironmentVariable("reservationsTable", "reservations-for-tests");
        Environment.SetEnvironmentVariable("reservationsTableGlobalIndex", "table-index-for-tests");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task HandleRequest_MissingOrBlankTableId_ReturnsBadRequest(string? tableId)
    {
        var path = tableId is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["id"] = tableId };
        var response = await Handle(new APIGatewayProxyRequest { PathParameters = path });

        Assert.Equal(400, response.StatusCode);
        _db.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-date")]
    public async Task HandleRequest_MissingOrInvalidStartTime_ReturnsBadRequest(string? startTime)
    {
        var query = startTime is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["startTime"] = startTime };
        var response = await Handle(Request(query));

        Assert.Equal(400, response.StatusCode);
        _db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_NoReservations_ReturnsAllNonOverlappingNinetyMinuteSlots()
    {
        QueryConfig? config = null;
        SetupReservations([], captured => config = captured);

        var response = await Handle(Request(StartAt("10:00")));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.Headers["Content-Type"]);
        using var body = JsonDocument.Parse(response.Body);
        var slots = body.RootElement.EnumerateArray().ToArray();
        Assert.Equal(7, slots.Length);
        Assert.Equal(new DateTime(2026, 6, 5, 10, 0, 0), slots[0].GetProperty("start").GetDateTime());
        Assert.Equal(new DateTime(2026, 6, 5, 11, 30, 0), slots[0].GetProperty("end").GetDateTime());
        Assert.Equal(new DateTime(2026, 6, 5, 11, 45, 0), slots[1].GetProperty("start").GetDateTime());
        Assert.Equal(new DateTime(2026, 6, 5, 22, 0, 0), slots[^1].GetProperty("end").GetDateTime());
        Assert.NotNull(config);
        Assert.Equal("reservations-for-tests", config.OverrideTableName);
        Assert.Equal("table-index-for-tests", config.IndexName);
        Assert.False(config.ConsistentRead);
    }

    [Fact]
    public async Task HandleRequest_ReservationOverlap_SkipsUntilFifteenMinuteGapAfterExistingReservation()
    {
        SetupReservations(
        [
            Reservation("booked", new DateTime(2026, 6, 5, 10, 0, 0), new DateTime(2026, 6, 5, 11, 30, 0))
        ]);

        var response = await Handle(Request(StartAt("10:00")));

        using var body = JsonDocument.Parse(response.Body);
        var starts = body.RootElement.EnumerateArray()
            .Select(slot => slot.GetProperty("start").GetDateTime()).ToArray();
        Assert.Equal(6, starts.Length);
        Assert.Equal(new DateTime(2026, 6, 5, 11, 45, 0), starts[0]);
        Assert.DoesNotContain(new DateTime(2026, 6, 5, 10, 0, 0), starts);
    }

    [Fact]
    public async Task HandleRequest_NonAlignedStartTime_RoundsUpToNextReservationBoundary()
    {
        SetupReservations([]);

        var response = await Handle(Request(StartAt("10:30")));

        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal(new DateTime(2026, 6, 5, 11, 30, 0),
            body.RootElement[0].GetProperty("start").GetDateTime());
    }

    [Fact]
    public async Task HandleRequest_StartAfterWorkingDay_ReturnsEmptySlotList()
    {
        SetupReservations([]);

        var response = await Handle(Request(StartAt("22:00")));

        using var body = JsonDocument.Parse(response.Body);
        Assert.Empty(body.RootElement.EnumerateArray());
    }

    private void SetupReservations(List<Reservation> reservations, Action<QueryConfig>? capture = null)
    {
        var search = new Mock<IAsyncSearch<Reservation>>();
        search.Setup(s => s.GetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(reservations);
        _db.Setup(db => db.QueryAsync<Reservation>("table-1", It.IsAny<QueryConfig>()))
            .Callback<object, QueryConfig>((_, config) => capture?.Invoke(config))
            .Returns(search.Object);
    }

    private Task<APIGatewayProxyResponse> Handle(APIGatewayProxyRequest request) =>
        new GetAvailableTimeSlotsHandler(_db.Object).HandleRequest(request, _context.Object);

    private static APIGatewayProxyRequest Request(Dictionary<string, string>? query = null) => new()
    {
        PathParameters = new Dictionary<string, string> { ["id"] = "table-1" },
        QueryStringParameters = query,
        RequestContext = new()
    };

    private static Dictionary<string, string> StartAt(string time) =>
        new() { ["startTime"] = $"2026-06-05T{time}:00" };

    private static Reservation Reservation(string id, DateTime start, DateTime end) => new()
    {
        Id = id,
        TableId = "table-1",
        WaiterId = "waiter-1",
        StartTime = start,
        EndTime = end,
        Status = ReservationStatus.Reserved
    };
}
