using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.Users;

public class EditUserHandler : IApiRequestHandler
{
    private readonly string _userPoolId = ResourceNames.UserPoolId;

    private readonly AmazonCognitoIdentityProviderClient _cognitoClient;

    public EditUserHandler(AmazonCognitoIdentityProviderClient cognitoClient)
    {
        _cognitoClient = cognitoClient ?? throw new ArgumentNullException(nameof(cognitoClient));
    }

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var (errorResponse, editUserRequest) = ValidateRequest(request, context);
        if (errorResponse is not null || editUserRequest is null)
        {
            return errorResponse!;
        }

        var username = request.RequestContext.Authorizer!.Claims!["cognito:username"];

        List<GroupType>? currentGroups = null;
        if (editUserRequest.Role is not null)
        {
            // This endpoint edits the authenticated user's own account. Use Cognito's current
            // memberships rather than the token's potentially stale groups claim to authorize
            // a role change.
            currentGroups = await ListGroupsForUserAsync(username);

            if (!currentGroups.Any(group =>
                    string.Equals(group.GroupName, Roles.Admin.ToString(), StringComparison.Ordinal)))
            {
                return ResponseBuilder.CreateErrorResponse(
                    403, "Only Admin users can change roles.", context.Logger);
            }
        }

        var updateRequest = new AdminUpdateUserAttributesRequest
        {
            UserPoolId = _userPoolId,
            Username = username,
            UserAttributes =
            [
                new AttributeType { Name = "given_name", Value = editUserRequest.FirstName },
                new AttributeType { Name = "family_name", Value = editUserRequest.LastName }
            ]
        };

        var result = await _cognitoClient.AdminUpdateUserAttributesAsync(updateRequest);
        if (result.HttpStatusCode != HttpStatusCode.OK)
        {
            return ResponseBuilder.CreateErrorResponse(
                (int)result.HttpStatusCode, "Failed to update user information.", context.Logger);
        }

        if (editUserRequest.Role is not null)
        {
            await UpdateUserRoleAsync(currentGroups!, username, editUserRequest.Role, context.Logger);
        }

        return ResponseBuilder.CreateSuccessResponse("User information updated successfully.");
    }

    private static (APIGatewayProxyResponse?, EditUserRequest?) ValidateRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (request.RequestContext.Authorizer?.Claims is not { } claims ||
            !claims.ContainsKey("sub") ||
            !claims.ContainsKey("cognito:username"))
        {
            context.Logger.LogError("[ERROR] User is not authenticated.");
            return (ResponseBuilder.CreateErrorResponse(401, "User is not authenticated.", context.Logger), null);
        }

        EditUserRequest? editUserRequest;
        try
        {
            editUserRequest = JsonSerializer.Deserialize<EditUserRequest>(request.Body);
            if (editUserRequest is null)
            {
                return (ResponseBuilder.CreateErrorResponse(400, "Invalid request body.", context.Logger), null);
            }
        }
        catch (JsonException)
        {
            context.Logger.LogError("[ERROR] Request body is null or invalid.");
            return (ResponseBuilder.CreateErrorResponse(400, "Invalid request body.", context.Logger), null);
        }

        try
        {
            PersonalDataValidator.ValidateName(editUserRequest.FirstName, "First name");
            PersonalDataValidator.ValidateName(editUserRequest.LastName, "Last name");
            if (editUserRequest.Role is not null &&
                editUserRequest.Role is not ("Admin" or "Customer" or "Waiter"))
            {
                return (ResponseBuilder.CreateErrorResponse(
                    400, "Role must be one of: Admin, Customer, Waiter.", context.Logger), null);
            }
        }
        catch (ValidationException e)
        {
            context.Logger.LogError($"[ERROR] {e.Message}");
            return (ResponseBuilder.CreateErrorResponse(400, e.Message, context.Logger), null);
        }

        return (null, editUserRequest);
    }

    private async Task<List<GroupType>> ListGroupsForUserAsync(string username)
    {
        var groups = new List<GroupType>();
        string? nextToken = null;

        do
        {
            var response = await _cognitoClient.AdminListGroupsForUserAsync(new AdminListGroupsForUserRequest
            {
                UserPoolId = _userPoolId,
                Username = username,
                NextToken = nextToken
            });

            groups.AddRange(response.Groups);
            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        return groups;
    }

    private async Task UpdateUserRoleAsync(
        List<GroupType> groups,
        string username,
        string role,
        ILambdaLogger logger)
    {
        var roleGroups = Enum.GetNames<Roles>();
        var existingRoleGroups = groups
            .Select(group => group.GroupName)
            .Where(groupName => roleGroups.Contains(groupName, StringComparer.Ordinal))
            .ToList();

        var previousRoleGroups = existingRoleGroups
            .Where(groupName => !string.Equals(groupName, role, StringComparison.Ordinal))
            .ToList();

        // Cognito has no transactional group update. If a removal fails after
        // earlier removals succeeded, reconcile against a fresh membership list
        // before restoring the previous role set.
        try
        {
            foreach (var existingRoleGroup in previousRoleGroups)
            {
                await _cognitoClient.AdminRemoveUserFromGroupAsync(new AdminRemoveUserFromGroupRequest
                {
                    UserPoolId = _userPoolId,
                    Username = username,
                    GroupName = existingRoleGroup
                });
            }
        }
        catch (Exception removalException)
        {
            await ReconcileRoleMembershipAfterFailureAsync(
                existingRoleGroups, previousRoleGroups, role, false, username, logger, removalException);
            throw;
        }

        if (!existingRoleGroups.Contains(role, StringComparer.Ordinal))
        {
            try
            {
                await _cognitoClient.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
                {
                    UserPoolId = _userPoolId,
                    Username = username,
                    GroupName = role
                });
            }
            catch (Exception addException)
            {
                await ReconcileRoleMembershipAfterFailureAsync(
                    existingRoleGroups, previousRoleGroups, role, true, username, logger, addException);
                throw;
            }
        }

        logger.LogLine($"[INFO] Updated Cognito role membership to {role}.");
    }

    private async Task ReconcileRoleMembershipAfterFailureAsync(
        List<string> originalRoleGroups,
        List<string> previousRoleGroups,
        string requestedRole,
        bool requestedRoleMayHaveBeenApplied,
        string username,
        ILambdaLogger logger,
        Exception operationException)
    {
        logger.LogLine(
            $"[ERROR] Cognito role update to {requestedRole} failed; re-reading role memberships before reconciliation.");

        List<GroupType> currentGroups;
        try
        {
            currentGroups = await ListGroupsForUserAsync(username);
        }
        catch (Exception readException)
        {
            logger.LogLine("[ERROR] Unable to re-read Cognito role memberships; no role compensation was attempted.");
            throw new AggregateException(
                "Cognito role update failed and current memberships could not be read for reconciliation.",
                operationException,
                readException);
        }

        var currentRoleGroups = currentGroups
            .Select(group => group.GroupName)
            .Where(groupName => groupName is not null &&
                                Enum.GetNames<Roles>().Contains(groupName, StringComparer.Ordinal))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        if (requestedRoleMayHaveBeenApplied && currentRoleGroups.Contains(requestedRole))
        {
            // The add may have succeeded even though its response timed out.
            // Keep the requested role and remove any previous roles still present;
            // restoring the snapshot here could leave the user with multiple roles.
            logger.LogLine(
                $"[INFO] Requested Cognito role {requestedRole} is present after the failed operation; reconciling away previous role memberships.");
            var cleanupExceptions = new List<Exception>();
            foreach (var previousRoleGroup in previousRoleGroups.Where(currentRoleGroups.Contains))
            {
                try
                {
                    await _cognitoClient.AdminRemoveUserFromGroupAsync(new AdminRemoveUserFromGroupRequest
                    {
                        UserPoolId = _userPoolId,
                        Username = username,
                        GroupName = previousRoleGroup
                    });
                }
                catch (Exception cleanupException)
                {
                    logger.LogLine(
                        $"[ERROR] Failed to remove previous Cognito role group {previousRoleGroup} while reconciling the requested role.");
                    cleanupExceptions.Add(cleanupException);
                }
            }

            if (cleanupExceptions.Count > 0)
            {
                throw new AggregateException(
                    "Cognito role update failed after the requested role appeared; cleanup of previous roles also failed.",
                    new[] { operationException }.Concat(cleanupExceptions));
            }

            logger.LogLine(
                $"[INFO] Previous Cognito roles were not restored because requested role {requestedRole} is already present.");
            return;
        }

        // A newly observed role indicates that another role change may have
        // happened concurrently. Do not overwrite that newer membership using
        // the earlier snapshot. The fresh read only reduces this risk: Cognito
        // has no conditional/transactional group mutation, so another writer can
        // still change memberships between this read and a compensating call.
        var unexpectedCurrentRoles = currentRoleGroups.Except(originalRoleGroups, StringComparer.Ordinal).ToList();
        if (unexpectedCurrentRoles.Count > 0)
        {
            logger.LogLine(
                $"[ERROR] Skipping restoration of previous Cognito roles because membership now includes a different role ({string.Join(", ", unexpectedCurrentRoles)}).");
            return;
        }

        var restorationExceptions = new List<Exception>();
        foreach (var previousRoleGroup in previousRoleGroups.Where(group => !currentRoleGroups.Contains(group)))
        {
            try
            {
                await _cognitoClient.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
                {
                    UserPoolId = _userPoolId,
                    Username = username,
                    GroupName = previousRoleGroup
                });
            }
            catch (Exception restorationException)
            {
                logger.LogLine(
                    $"[ERROR] Failed to restore previous Cognito role group {previousRoleGroup} after role update failure.");
                restorationExceptions.Add(restorationException);
            }
        }

        if (restorationExceptions.Count > 0)
        {
            throw new AggregateException(
                "Cognito role update failed and one or more previous role groups could not be restored.",
                new[] { operationException }.Concat(restorationExceptions));
        }

        logger.LogLine("[INFO] Reconciled Cognito membership to the previous role groups after role update failure.");
    }
}