using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Collections.Generic;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.GuestSessions;

public class GetAllSessionsForGroupHandlerTests
{
    private readonly Mock<IGuestSessionService> _guestSessionService = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public GetAllSessionsForGroupHandlerTests()
    {
        _context.Setup(context => context.Logger).Returns(_logger.Object);
    }

    [Fact]
    public async Task HandleRequest_ValidRequest_ReturnsCamelCaseGroupSessionsJson()
    {
        const string userId = "owner-1";
        const string groupReservationId = "group-1";
        var snapshot = new GetAllSessionsResponse(
            [
                new Participant(
                    "session-1",
                    ParticipantType.Owner,
                    "Owner",
                    [],
                    12.50m,
                    true)
            ],
            12.50m);

        _guestSessionService
            .Setup(service => service.GetAllSessionsAsync(userId, groupReservationId))
            .ReturnsAsync(snapshot);

        var handler = new GetAllSessionsForGroupHandler(_guestSessionService.Object);
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                ["groupReservationId"] = groupReservationId
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { ["sub"] = userId }
                }
            }
        };

        var result = await handler.HandleRequest(request, _context.Object);

        Assert.Equal(200, result.StatusCode);
        using var response = JsonDocument.Parse(result.Body);
        var root = response.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal(12.50m, root.GetProperty("total").GetDecimal());

        var participants = root.GetProperty("participants");
        Assert.Equal(JsonValueKind.Array, participants.ValueKind);
        var participant = Assert.Single(participants.EnumerateArray());
        Assert.Equal(6, participant.EnumerateObject().Count());
        Assert.Equal("session-1", participant.GetProperty("id").GetString());
        Assert.Equal("Owner", participant.GetProperty("type").GetString());
        Assert.Equal("Owner", participant.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Array, participant.GetProperty("items").ValueKind);
        Assert.Empty(participant.GetProperty("items").EnumerateArray());
        Assert.Equal(12.50m, participant.GetProperty("total").GetDecimal());
        Assert.True(participant.GetProperty("isSubmitted").GetBoolean());
    }
}
