using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.Users;
using Moq;
using System.Net;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Users;

[Collection("ResourceNamesEnvironment")]
public class EditUserHandlerTests
{
    private readonly Mock<AmazonCognitoIdentityProviderClient> _cognito = new(RegionEndpoint.USWest2);
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public EditUserHandlerTests()
    {
        _context.SetupGet(c => c.Logger).Returns(_logger.Object);
        Environment.SetEnvironmentVariable("COGNITO_USER_POOL_ID", "pool-for-tests");
    }

    [Fact]
    public async Task HandleRequest_MissingSubClaim_ReturnsUnauthorized()
    {
        var response = await Handle(Request("""{"firstName":"Alex","lastName":"Smith"}""", new Dictionary<string, string>
        {
            ["email"] = "alex@example.com"
        }));

        Assert.Equal(401, response.StatusCode);
        _cognito.Verify(c => c.AdminUpdateUserAttributesAsync(
            It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("{")]
    public async Task HandleRequest_MalformedBody_ReturnsBadRequest(string body)
    {
        var response = await Handle(Request(body, new Dictionary<string, string> { ["sub"] = "user-1" }));

        Assert.Equal(400, response.StatusCode);
        _cognito.Verify(c => c.AdminUpdateUserAttributesAsync(
            It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "Smith")]
    [InlineData("A", "Smith")]
    [InlineData("Жанна", "Smith")]
    [InlineData("Alex", "")]
    [InlineData("Alex", "Smith 2")]
    public async Task HandleRequest_InvalidName_ReturnsBadRequest(string firstName, string lastName)
    {
        var claims = new Dictionary<string, string> { ["sub"] = "user-1" };
        var body = JsonSerializer.Serialize(new { firstName, lastName });

        var response = await Handle(Request(body, claims));

        Assert.Equal(400, response.StatusCode);
        _cognito.Verify(c => c.AdminUpdateUserAttributesAsync(
            It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_ValidClaimsAndNames_UpdatesCognitoAttributes()
    {
        AdminUpdateUserAttributesRequest? captured = null;
        _cognito.Setup(c => c.AdminUpdateUserAttributesAsync(
                It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AdminUpdateUserAttributesRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new AdminUpdateUserAttributesResponse { HttpStatusCode = HttpStatusCode.OK });

        var response = await Handle(Request(
            """{"firstName":"Alex","lastName":"O'Neil"}""",
            new Dictionary<string, string> { ["sub"] = "user-1", ["email"] = "alex@example.com" }));

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(captured);
        Assert.Equal("pool-for-tests", captured.UserPoolId);
        Assert.Equal("alex@example.com", captured.Username);
        Assert.Equal("given_name", captured.UserAttributes[0].Name);
        Assert.Equal("Alex", captured.UserAttributes[0].Value);
        Assert.Equal("family_name", captured.UserAttributes[1].Name);
        Assert.Equal("O'Neil", captured.UserAttributes[1].Value);
    }

    [Fact]
    public async Task HandleRequest_EmailClaimMissing_ReturnsUnauthorizedWithoutCognitoCall()
    {
        var response = await Handle(Request(
            """{"firstName":"Alex","lastName":"Smith"}""",
            new Dictionary<string, string> { ["sub"] = "user-1" }));

        Assert.Equal(401, response.StatusCode);
        _cognito.Verify(c => c.AdminUpdateUserAttributesAsync(
            It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_CognitoReturnsNonOkStatus_MapsStatusToErrorResponse()
    {
        _cognito.Setup(c => c.AdminUpdateUserAttributesAsync(
                It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUpdateUserAttributesResponse { HttpStatusCode = HttpStatusCode.Forbidden });

        var response = await Handle(Request(
            """{"firstName":"Alex","lastName":"Smith"}""",
            new Dictionary<string, string> { ["sub"] = "user-1", ["email"] = "alex@example.com" }));

        Assert.Equal(403, response.StatusCode);
        Assert.Contains("Failed to update user", response.Body);
    }

    private Task<APIGatewayProxyResponse> Handle(APIGatewayProxyRequest request) =>
        new EditUserHandler(_cognito.Object).HandleRequest(request, _context.Object);

    private static APIGatewayProxyRequest Request(string body, Dictionary<string, string> claims) => new()
    {
        Body = body,
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext { Claims = claims }
        }
    };
}
