using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.Feedbacks;
using APIHandler.Models.Feedbacks;
using APIHandler.Services.Auth;
using APIHandler.Services.Feedbacks;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Feedbacks;

public class GetFeedbackForLocationHandlerTests
{
    private readonly Mock<IFeedbackService> _feedback = new();
    private readonly Mock<IUserService> _users = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public GetFeedbackForLocationHandlerTests() => _context.SetupGet(c => c.Logger).Returns(_logger.Object);

    [Theory]
    [InlineData("type")]
    [InlineData("sort")]
    [InlineData("page")]
    [InlineData("locationId")]
    public async Task HandleRequest_MissingRequiredParameter_ReturnsBadRequest(string missingKey)
    {
        var response = await Handle(QueryExcept(missingKey));

        Assert.Equal(400, response.StatusCode);
        _feedback.VerifyNoOtherCalls();
        _users.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("page", "0")]
    [InlineData("page", "one")]
    [InlineData("type", "unknown")]
    [InlineData("sort", "random")]
    public async Task HandleRequest_InvalidQueryValue_ReturnsBadRequest(string key, string value)
    {
        var query = Query();
        query[key] = value;

        var response = await Handle(query);

        Assert.Equal(400, response.StatusCode);
        _feedback.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_ValidQuery_EnrichesNamedAndAnonymousFeedback()
    {
        var named = Feedback("user-1");
        var anonymous = Feedback(null);
        _feedback.Setup(s => s.GetFeedbackForLocationAsync("location-1", 2, FeedbackType.Service, SortingOrder.NewestFirst))
            .ReturnsAsync(new FeedbackResponse { Items = [named, anonymous], TotalPages = 4, CurrentPage = 2 });
        _users.Setup(s => s.GetUserName("user-1")).ReturnsAsync("Jordan Lee");

        var response = await Handle(Query());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Jordan Lee", named.Username);
        Assert.Equal("Anonymous User", anonymous.Username);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal(4, body.RootElement.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("currentPage").GetInt32());
        Assert.Equal("Jordan Lee", body.RootElement.GetProperty("items")[0].GetProperty("username").GetString());
        Assert.Equal("Anonymous User", body.RootElement.GetProperty("items")[1].GetProperty("username").GetString());
        _users.Verify(s => s.GetUserName("user-1"), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_UserNameUnavailable_UsesAnonymousFallback()
    {
        var item = Feedback("deleted-user");
        _feedback.Setup(s => s.GetFeedbackForLocationAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<FeedbackType>(), It.IsAny<SortingOrder>()))
            .ReturnsAsync(new FeedbackResponse { Items = [item] });
        _users.Setup(s => s.GetUserName("deleted-user")).ReturnsAsync((string?)null);

        var response = await Handle(Query());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Anonymous User", item.Username);
    }

    [Fact]
    public async Task HandleRequest_FeedbackServiceValidationFailure_ReturnsBadRequest()
    {
        _feedback.Setup(s => s.GetFeedbackForLocationAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<FeedbackType>(), It.IsAny<SortingOrder>()))
            .ThrowsAsync(new ValidationException("Location feedback is unavailable."));

        var response = await Handle(Query());

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("unavailable", response.Body);
    }

    private Task<APIGatewayProxyResponse> Handle(Dictionary<string, string> query) =>
        new GetFeedbackForLocationHandler(_feedback.Object, _users.Object)
            .HandleRequest(new APIGatewayProxyRequest { QueryStringParameters = query }, _context.Object);

    private static Dictionary<string, string> Query() => new()
    {
        ["locationId"] = "location-1",
        ["type"] = "Service",
        ["sort"] = "NewestFirst",
        ["page"] = "2"
    };

    private static Dictionary<string, string> QueryExcept(string excluded)
    {
        var query = Query();
        query.Remove(excluded);
        return query;
    }

    private static FeedbackResponseItem Feedback(string? userId) => new()
    {
        ReservationId = "reservation-1",
        LocationId = "location-1",
        UserId = userId,
        Rating = 5,
        Type = FeedbackType.Service
    };
}
