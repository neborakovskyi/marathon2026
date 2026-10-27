using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Runtime;
using APIHandler.Handlers;
using APIHandler.Models;
using APIHandler.Services.Waiters;
using Moq;
using System.Net;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class RegistrationHandlerTests
{
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    static RegistrationHandlerTests()
    {
        Environment.SetEnvironmentVariable("COGNITO_USER_POOL_ID", "test-user-pool");
        Environment.SetEnvironmentVariable("COGNITO_USER_POOL_CLIENT_ID", "test-client");
    }

    public RegistrationHandlerTests()
    {
        _context.Setup(context => context.Logger).Returns(_logger.Object);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Guest")]
    [InlineData("admin")]
    [InlineData("Other")]
    public async Task HandleRequest_UnsupportedRole_ReturnsBadRequestWithoutCreatingUser(string role)
    {
        var cognitoClient = CreateCognitoClient();

        var response = await CreateHandler(cognitoClient).HandleRequest(
            CreateRequest($$"""{"firstName":"Jane","lastName":"Doe","email":"jane@example.com","password":"Secure1!","confirmPassword":"Secure1!","role":"{{role}}"}"""),
            _context.Object);

        Assert.Equal(400, response.StatusCode);
        cognitoClient.Verify(client => client.SignUpAsync(
            It.IsAny<SignUpRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_WaiterRole_AddsUserToWaiterGroup()
    {
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.SignUpAsync(It.IsAny<SignUpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignUpResponse { HttpStatusCode = HttpStatusCode.OK });
        cognitoClient
            .Setup(client => client.AdminConfirmSignUpAsync(
                It.IsAny<AdminConfirmSignUpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminConfirmSignUpResponse());
        cognitoClient
            .Setup(client => client.ListGroupsAsync(It.IsAny<ListGroupsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListGroupsResponse { Groups = [new GroupType { GroupName = "Waiter" }] });
        cognitoClient
            .Setup(client => client.AdminAddUserToGroupAsync(
                It.IsAny<AdminAddUserToGroupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAddUserToGroupResponse());

        var response = await CreateHandler(cognitoClient).HandleRequest(
            CreateRequest("""{"firstName":"Jane","lastName":"Doe","email":"jane@example.com","password":"Secure1!","confirmPassword":"Secure1!","role":"Waiter"}"""),
            _context.Object);

        Assert.Equal(200, response.StatusCode);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request => request.GroupName == "Waiter"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RegistrationRequest_OmittedRole_DefaultsToCustomer()
    {
        var request = JsonSerializer.Deserialize<RegistrationRequest>(
            """{"firstName":"Jane","lastName":"Doe","email":"jane@example.com","password":"Secure1!","confirmPassword":"Secure1!"}""");

        Assert.Equal("Customer", request!.Role);
    }

    private static Mock<AmazonCognitoIdentityProviderClient> CreateCognitoClient() =>
        new(new AnonymousAWSCredentials(), new AmazonCognitoIdentityProviderConfig
        {
            RegionEndpoint = RegionEndpoint.USWest2
        });

    private static RegistrationHandler CreateHandler(Mock<AmazonCognitoIdentityProviderClient> cognitoClient) =>
        new(Mock.Of<IWaiterService>(), cognitoClient.Object);

    private static APIGatewayProxyRequest CreateRequest(string body) => new() { Body = body };
}
