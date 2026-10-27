using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.Users;
using Moq;
using System.Net;

namespace APIHandler.Tests.Handlers.Users;

public class EditUserHandlerTests
{
    private const string UserEmail = "person@example.com";
    private const string CognitoUsername = "canonical-cognito-username";
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    static EditUserHandlerTests()
    {
        Environment.SetEnvironmentVariable("COGNITO_USER_POOL_ID", "test-user-pool");
    }

    public EditUserHandlerTests()
    {
        _context.Setup(context => context.Logger).Returns(_logger.Object);
    }

    [Fact]
    public async Task HandleRequest_NameOnlyUpdate_UsesCanonicalCognitoUsername()
    {
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminUpdateUserAttributesAsync(
                It.Is<AdminUpdateUserAttributesRequest>(request =>
                    request.Username == CognitoUsername &&
                    request.UserAttributes.Count == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUpdateUserAttributesResponse { HttpStatusCode = HttpStatusCode.OK });

        var response = await CreateHandler(cognitoClient).HandleRequest(
            CreateRequest("""{"firstName":"Jane","lastName":"Doe"}""", "Customer"),
            _context.Object);

        Assert.Equal(200, response.StatusCode);
        cognitoClient.Verify(client => client.AdminUpdateUserAttributesAsync(
            It.Is<AdminUpdateUserAttributesRequest>(request => request.Username == CognitoUsername),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminListGroupsForUserAsync(
            It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_AdminCallerChangesRole_UpdatesCognitoGroups()
    {
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminUpdateUserAttributesAsync(
                It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUpdateUserAttributesResponse { HttpStatusCode = HttpStatusCode.OK });
        cognitoClient
            .Setup(client => client.AdminListGroupsForUserAsync(
                It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "Admin" },
                    new GroupType { GroupName = "Customer" },
                    new GroupType { GroupName = "OtherGroup" }
                ]
            });
        cognitoClient
            .Setup(client => client.AdminAddUserToGroupAsync(
                It.Is<AdminAddUserToGroupRequest>(request => request.GroupName == "Waiter"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAddUserToGroupResponse());
        cognitoClient
            .Setup(client => client.AdminRemoveUserFromGroupAsync(
                It.Is<AdminRemoveUserFromGroupRequest>(request => request.GroupName == "Admin"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRemoveUserFromGroupResponse());
        cognitoClient
            .Setup(client => client.AdminRemoveUserFromGroupAsync(
                It.Is<AdminRemoveUserFromGroupRequest>(request => request.GroupName == "Customer"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRemoveUserFromGroupResponse());

        var response = await CreateHandler(cognitoClient).HandleRequest(
            CreateRequest("""{"firstName":"Jane","lastName":"Doe","role":"Waiter"}""", "Admin"),
            _context.Object);

        Assert.Equal(200, response.StatusCode);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request =>
                request.GroupName == "Waiter" && request.Username == CognitoUsername),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminRemoveUserFromGroupAsync(
            It.Is<AdminRemoveUserFromGroupRequest>(request =>
                request.GroupName == "Admin" && request.Username == CognitoUsername),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminRemoveUserFromGroupAsync(
            It.Is<AdminRemoveUserFromGroupRequest>(request =>
                request.GroupName == "Customer" && request.Username == CognitoUsername),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminRemoveUserFromGroupAsync(
            It.Is<AdminRemoveUserFromGroupRequest>(request => request.GroupName == "OtherGroup"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_AdminGroupOnLaterPage_AuthorizesRoleChange()
    {
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminUpdateUserAttributesAsync(
                It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUpdateUserAttributesResponse { HttpStatusCode = HttpStatusCode.OK });
        cognitoClient
            .SetupSequence(client => client.AdminListGroupsForUserAsync(
                It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups = [new GroupType { GroupName = "OtherGroup" }],
                NextToken = "membership-page-2"
            })
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "Admin" },
                    new GroupType { GroupName = "Customer" },
                    new GroupType { GroupName = "AnotherUnrelatedGroup" }
                ]
            });
        cognitoClient
            .Setup(client => client.AdminRemoveUserFromGroupAsync(
                It.IsAny<AdminRemoveUserFromGroupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRemoveUserFromGroupResponse());
        cognitoClient
            .Setup(client => client.AdminAddUserToGroupAsync(
                It.IsAny<AdminAddUserToGroupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAddUserToGroupResponse());

        var response = await CreateHandler(cognitoClient).HandleRequest(
            CreateRequest("""{"firstName":"Jane","lastName":"Doe","role":"Waiter"}""", "Admin"),
            _context.Object);

        Assert.Equal(200, response.StatusCode);
        cognitoClient.Verify(client => client.AdminListGroupsForUserAsync(
            It.Is<AdminListGroupsForUserRequest>(request =>
                request.Username == CognitoUsername && request.NextToken == null),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminListGroupsForUserAsync(
            It.Is<AdminListGroupsForUserRequest>(request =>
                request.Username == CognitoUsername && request.NextToken == "membership-page-2"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminRemoveUserFromGroupAsync(
            It.Is<AdminRemoveUserFromGroupRequest>(request =>
                request.Username == CognitoUsername &&
                (request.GroupName == "Admin" || request.GroupName == "Customer")),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        cognitoClient.Verify(client => client.AdminRemoveUserFromGroupAsync(
            It.Is<AdminRemoveUserFromGroupRequest>(request =>
                request.GroupName == "OtherGroup" || request.GroupName == "AnotherUnrelatedGroup"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_ReconciliationFindsUnexpectedRoleOnLaterPage_SkipsRestoration()
    {
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminUpdateUserAttributesAsync(
                It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUpdateUserAttributesResponse { HttpStatusCode = HttpStatusCode.OK });
        cognitoClient
            .SetupSequence(client => client.AdminListGroupsForUserAsync(
                It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups = [new GroupType { GroupName = "Admin" }],
                NextToken = "initial-page-2"
            })
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "Customer" },
                    new GroupType { GroupName = "OtherGroup" }
                ]
            })
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups = [new GroupType { GroupName = "OtherGroup" }],
                NextToken = "reconciliation-page-2"
            })
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups = [new GroupType { GroupName = "Waiter" }]
            });
        cognitoClient
            .SetupSequence(client => client.AdminRemoveUserFromGroupAsync(
                It.IsAny<AdminRemoveUserFromGroupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRemoveUserFromGroupResponse())
            .ThrowsAsync(new AmazonServiceException("Cognito removal failed."));

        await Assert.ThrowsAsync<AmazonServiceException>(() =>
            CreateHandler(cognitoClient).HandleRequest(
                CreateRequest("""{"firstName":"Jane","lastName":"Doe","role":"Waiter"}""", "Admin"),
                _context.Object));

        cognitoClient.Verify(client => client.AdminListGroupsForUserAsync(
            It.Is<AdminListGroupsForUserRequest>(request =>
                request.Username == CognitoUsername && request.NextToken == "initial-page-2"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminListGroupsForUserAsync(
            It.Is<AdminListGroupsForUserRequest>(request =>
                request.Username == CognitoUsername && request.NextToken == "reconciliation-page-2"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.IsAny<AdminAddUserToGroupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_RoleRemovalFails_DoesNotAddRequestedRole()
    {
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminUpdateUserAttributesAsync(
                It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUpdateUserAttributesResponse { HttpStatusCode = HttpStatusCode.OK });
        cognitoClient
            .SetupSequence(client => client.AdminListGroupsForUserAsync(
                It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "Admin" },
                    new GroupType { GroupName = "Customer" }
                ]
            })
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "Customer" },
                    new GroupType { GroupName = "OtherGroup" }
                ]
            });
        cognitoClient
            .SetupSequence(client => client.AdminRemoveUserFromGroupAsync(
                It.IsAny<AdminRemoveUserFromGroupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRemoveUserFromGroupResponse())
            .ThrowsAsync(new AmazonServiceException("Cognito removal failed."));

        await Assert.ThrowsAsync<AmazonServiceException>(() =>
            CreateHandler(cognitoClient).HandleRequest(
                CreateRequest("""{"firstName":"Jane","lastName":"Doe","role":"Waiter"}""", "Admin"),
                _context.Object));

        cognitoClient.Verify(client => client.AdminRemoveUserFromGroupAsync(
            It.Is<AdminRemoveUserFromGroupRequest>(request =>
                request.Username == CognitoUsername && request.GroupName == "Admin"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminRemoveUserFromGroupAsync(
            It.Is<AdminRemoveUserFromGroupRequest>(request =>
                request.Username == CognitoUsername && request.GroupName == "Customer"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request =>
                request.Username == CognitoUsername && request.GroupName == "Admin"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request => request.GroupName == "Waiter"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_RequestedRoleAddFails_RestoresPreviousRoleGroups()
    {
        var addException = new AmazonServiceException("Cognito add failed.");
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminUpdateUserAttributesAsync(
                It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUpdateUserAttributesResponse { HttpStatusCode = HttpStatusCode.OK });
        cognitoClient
            .SetupSequence(client => client.AdminListGroupsForUserAsync(
                It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "Admin" },
                    new GroupType { GroupName = "Customer" },
                    new GroupType { GroupName = "OtherGroup" }
                ]
            })
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups = [new GroupType { GroupName = "OtherGroup" }]
            });
        cognitoClient
            .Setup(client => client.AdminRemoveUserFromGroupAsync(
                It.IsAny<AdminRemoveUserFromGroupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRemoveUserFromGroupResponse());
        cognitoClient
            .Setup(client => client.AdminAddUserToGroupAsync(
                It.Is<AdminAddUserToGroupRequest>(request => request.GroupName == "Waiter"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(addException);
        cognitoClient
            .Setup(client => client.AdminAddUserToGroupAsync(
                It.Is<AdminAddUserToGroupRequest>(request => request.GroupName == "Admin" || request.GroupName == "Customer"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAddUserToGroupResponse());

        var exception = await Assert.ThrowsAsync<AmazonServiceException>(() =>
            CreateHandler(cognitoClient).HandleRequest(
                CreateRequest("""{"firstName":"Jane","lastName":"Doe","role":"Waiter"}""", "Admin"),
                _context.Object));

        Assert.Same(addException, exception);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request =>
                request.Username == CognitoUsername && request.GroupName == "Waiter"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request =>
                request.Username == CognitoUsername && request.GroupName == "Admin"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request =>
                request.Username == CognitoUsername && request.GroupName == "Customer"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminRemoveUserFromGroupAsync(
            It.Is<AdminRemoveUserFromGroupRequest>(request => request.GroupName == "OtherGroup"),
            It.IsAny<CancellationToken>()), Times.Never);
        _logger.Verify(logger => logger.LogLine(It.Is<string>(message =>
            message.Contains("re-reading role memberships", StringComparison.Ordinal))), Times.Once);
        _logger.Verify(logger => logger.LogLine(It.Is<string>(message =>
            message.Contains("Reconciled Cognito membership to the previous role groups", StringComparison.Ordinal))), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_RequestedRoleAddThrowsAfterApplying_DoesNotRestorePreviousRoles()
    {
        var addException = new AmazonServiceException("Cognito add timed out after applying the request.");
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminUpdateUserAttributesAsync(
                It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUpdateUserAttributesResponse { HttpStatusCode = HttpStatusCode.OK });
        cognitoClient
            .SetupSequence(client => client.AdminListGroupsForUserAsync(
                It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "Admin" },
                    new GroupType { GroupName = "Customer" },
                    new GroupType { GroupName = "OtherGroup" }
                ]
            })
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "Waiter" },
                    new GroupType { GroupName = "OtherGroup" }
                ]
            });
        cognitoClient
            .Setup(client => client.AdminRemoveUserFromGroupAsync(
                It.IsAny<AdminRemoveUserFromGroupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRemoveUserFromGroupResponse());
        cognitoClient
            .Setup(client => client.AdminAddUserToGroupAsync(
                It.Is<AdminAddUserToGroupRequest>(request => request.GroupName == "Waiter"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(addException);

        var exception = await Assert.ThrowsAsync<AmazonServiceException>(() =>
            CreateHandler(cognitoClient).HandleRequest(
                CreateRequest("""{"firstName":"Jane","lastName":"Doe","role":"Waiter"}""", "Admin"),
                _context.Object));

        Assert.Same(addException, exception);
        cognitoClient.Verify(client => client.AdminListGroupsForUserAsync(
            It.Is<AdminListGroupsForUserRequest>(request => request.Username == CognitoUsername),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request => request.GroupName == "Waiter"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request =>
                request.GroupName == "Admin" || request.GroupName == "Customer"),
            It.IsAny<CancellationToken>()), Times.Never);
        cognitoClient.Verify(client => client.AdminRemoveUserFromGroupAsync(
            It.Is<AdminRemoveUserFromGroupRequest>(request => request.GroupName == "OtherGroup"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_RequestedRoleAddAndRestorationFail_SurfacesBothFailures()
    {
        var addException = new AmazonServiceException("Cognito add failed.");
        var restorationException = new AmazonServiceException("Cognito restoration failed.");
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminUpdateUserAttributesAsync(
                It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUpdateUserAttributesResponse { HttpStatusCode = HttpStatusCode.OK });
        cognitoClient
            .SetupSequence(client => client.AdminListGroupsForUserAsync(
                It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "Admin" },
                    new GroupType { GroupName = "Customer" }
                ]
            })
            .ReturnsAsync(new AdminListGroupsForUserResponse { Groups = [] });
        cognitoClient
            .Setup(client => client.AdminRemoveUserFromGroupAsync(
                It.IsAny<AdminRemoveUserFromGroupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRemoveUserFromGroupResponse());
        cognitoClient
            .Setup(client => client.AdminAddUserToGroupAsync(
                It.Is<AdminAddUserToGroupRequest>(request => request.GroupName == "Waiter"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(addException);
        cognitoClient
            .Setup(client => client.AdminAddUserToGroupAsync(
                It.Is<AdminAddUserToGroupRequest>(request => request.GroupName == "Admin"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAddUserToGroupResponse());
        cognitoClient
            .Setup(client => client.AdminAddUserToGroupAsync(
                It.Is<AdminAddUserToGroupRequest>(request => request.GroupName == "Customer"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(restorationException);

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            CreateHandler(cognitoClient).HandleRequest(
                CreateRequest("""{"firstName":"Jane","lastName":"Doe","role":"Waiter"}""", "Admin"),
                _context.Object));

        Assert.Contains(addException, exception.InnerExceptions);
        Assert.Contains(restorationException, exception.InnerExceptions);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request =>
                request.Username == CognitoUsername && request.GroupName == "Admin"),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(request =>
                request.Username == CognitoUsername && request.GroupName == "Customer"),
            It.IsAny<CancellationToken>()), Times.Once);
        _logger.Verify(logger => logger.LogLine(It.Is<string>(message =>
            message.Contains("Failed to restore previous Cognito role group Customer", StringComparison.Ordinal))), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_NonAdminCallerRequestsRoleChange_ForbiddenWithoutCognitoMutation()
    {
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminListGroupsForUserAsync(
                It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups = [new GroupType { GroupName = "Customer" }]
            });

        var response = await CreateHandler(cognitoClient).HandleRequest(
            CreateRequest("""{"firstName":"Jane","lastName":"Doe","role":"Waiter"}""", "Customer"),
            _context.Object);

        Assert.Equal(403, response.StatusCode);
        cognitoClient.Verify(client => client.AdminUpdateUserAttributesAsync(
            It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.IsAny<AdminAddUserToGroupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_StaleAdminTokenAfterSelfDemotion_CannotRestoreAdminRole()
    {
        var cognitoClient = CreateCognitoClient();
        cognitoClient
            .Setup(client => client.AdminListGroupsForUserAsync(
                It.IsAny<AdminListGroupsForUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups = [new GroupType { GroupName = "Customer" }]
            });

        var response = await CreateHandler(cognitoClient).HandleRequest(
            CreateRequest("""{"firstName":"Jane","lastName":"Doe","role":"Admin"}""", "Admin"),
            _context.Object);

        Assert.Equal(403, response.StatusCode);
        cognitoClient.Verify(client => client.AdminListGroupsForUserAsync(
            It.Is<AdminListGroupsForUserRequest>(request => request.Username == CognitoUsername),
            It.IsAny<CancellationToken>()), Times.Once);
        cognitoClient.Verify(client => client.AdminUpdateUserAttributesAsync(
            It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        cognitoClient.Verify(client => client.AdminAddUserToGroupAsync(
            It.IsAny<AdminAddUserToGroupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        cognitoClient.Verify(client => client.AdminRemoveUserFromGroupAsync(
            It.IsAny<AdminRemoveUserFromGroupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Guest")]
    [InlineData("Manager")]
    [InlineData("admin")]
    public async Task HandleRequest_UnsupportedRole_ReturnsBadRequest(string role)
    {
        var cognitoClient = CreateCognitoClient();

        var response = await CreateHandler(cognitoClient).HandleRequest(
            CreateRequest($$"""{"firstName":"Jane","lastName":"Doe","role":"{{role}}"}""", "Admin"),
            _context.Object);

        Assert.Equal(400, response.StatusCode);
        cognitoClient.Verify(client => client.AdminUpdateUserAttributesAsync(
            It.IsAny<AdminUpdateUserAttributesRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<AmazonCognitoIdentityProviderClient> CreateCognitoClient() =>
        new(new AnonymousAWSCredentials(), new AmazonCognitoIdentityProviderConfig
        {
            RegionEndpoint = RegionEndpoint.USWest2
        });

    private static EditUserHandler CreateHandler(Mock<AmazonCognitoIdentityProviderClient> cognitoClient) =>
        new(cognitoClient.Object);

    private static APIGatewayProxyRequest CreateRequest(string body, string callerRole) =>
        new()
        {
            Body = body,
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>
                    {
                        ["sub"] = "user-id",
                        ["email"] = UserEmail,
                        ["cognito:username"] = CognitoUsername,
                        ["cognito:groups"] = callerRole
                    }
                }
            }
        };
}
