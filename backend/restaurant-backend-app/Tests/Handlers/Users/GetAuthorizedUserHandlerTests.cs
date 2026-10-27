using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.Users;
using APIHandler.Models.Users;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Users;

public class GetAuthorizedUserHandlerTests
{
    private const string CognitoUsername = "canonical-cognito-username";
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    static GetAuthorizedUserHandlerTests()
    {
        Environment.SetEnvironmentVariable("COGNITO_USER_POOL_ID", "test-user-pool");
    }

    public GetAuthorizedUserHandlerTests()
    {
        _context.Setup(context => context.Logger).Returns(_logger.Object);
    }

    [Fact]
    public async Task HandleRequest_RoleGroupOnLaterPage_ReturnsActualRole()
    {
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminGetUserAsync(
                It.Is<AdminGetUserRequest>(request =>
                    request.Username == CognitoUsername && request.UserPoolId == "test-user-pool"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGetUserResponse
            {
                UserAttributes =
                [
                    new AttributeType { Name = "given_name", Value = "Jane" },
                    new AttributeType { Name = "family_name", Value = "Doe" },
                    new AttributeType { Name = "email", Value = "jane@example.com" }
                ]
            });
        cognitoClient
            .SetupSequence(client => client.AdminListGroupsForUserAsync(
                It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups = [new GroupType { GroupName = "UnrelatedGroup" }],
                NextToken = "membership-page-2"
            })
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups = [new GroupType { GroupName = "Admin" }]
            });

        var response = await new GetAuthorizedUserHandler(cognitoClient.Object).HandleRequest(
            CreateRequest(),
            _context.Object);

        Assert.Equal(200, response.StatusCode);
        var user = JsonSerializer.Deserialize<UserDto>(response.Body!);
        Assert.NotNull(user);
        Assert.Equal("Admin", user.Role);
        cognitoClient.Verify(client => client.AdminListGroupsForUserAsync(
            It.Is<AdminListGroupsForUserRequest>(request =>
                request.Username == CognitoUsername &&
                request.UserPoolId == "test-user-pool" &&
                request.NextToken == null),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminListGroupsForUserAsync(
            It.Is<AdminListGroupsForUserRequest>(request =>
                request.Username == CognitoUsername &&
                request.UserPoolId == "test-user-pool" &&
                request.NextToken == "membership-page-2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<AmazonCognitoIdentityProviderClient> CreateCognitoClient() =>
        new(new AnonymousAWSCredentials(), new AmazonCognitoIdentityProviderConfig
        {
            RegionEndpoint = RegionEndpoint.USWest2
        });

    private static APIGatewayProxyRequest CreateRequest() =>
        new()
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>
                    {
                        ["sub"] = "user-id",
                        ["cognito:username"] = CognitoUsername
                    }
                }
            }
        };
}
