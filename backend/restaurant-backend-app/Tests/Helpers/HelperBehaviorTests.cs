using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.Users;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ValidationException = APIHandler.Exceptions.ValidationException;

namespace APIHandler.Tests.Helpers;

public class HelperBehaviorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void DataValidationHelper_IsMissing_NullOrWhitespace_ReturnsTrue(string? value)
        => Assert.True(DataValidationHelper.IsMissing(value));

    [Theory]
    [InlineData("value")]
    [InlineData("  x ")]
    public void DataValidationHelper_IsMissing_NonWhitespace_ReturnsFalse(string value)
        => Assert.False(DataValidationHelper.IsMissing(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RequestExtension_TryGetPathValue_MissingOrEmptyValue_ReturnsFalse(string? pathValue)
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = pathValue is null
                ? null
                : new Dictionary<string, string> { ["id"] = pathValue }
        };

        Assert.False(request.TryGetPathValue("id", out var value));
        Assert.Equal(pathValue, value);
    }

    [Fact]
    public void RequestExtension_TryGetPathValue_PresentValue_ReturnsValue()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { ["id"] = "dish-1" }
        };

        Assert.True(request.TryGetPathValue("id", out var value));
        Assert.Equal("dish-1", value);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("Admin", "admin", true)]
    [InlineData("Admin", "Customer", false)]
    public void EqualityHelper_IgnoreCase_UsesOrdinalCaseInsensitiveComparison(
        string? left, string? right, bool expected)
    {
        Assert.Equal(expected, EqualityHelper.EqualsIgnoreCase(left, right));
        Assert.Equal(!expected, EqualityHelper.NotEqualsIgnoreCase(left, right));
    }

    [Theory]
    [InlineData("Admin", "Admin", true)]
    [InlineData("Admin", "admin", false)]
    [InlineData(null, null, true)]
    public void EqualityHelper_CaseSensitive_UsesOrdinalComparison(
        string? left, string? right, bool expected)
    {
        Assert.Equal(expected, EqualityHelper.EqualsCaseSensitive(left, right));
        Assert.Equal(!expected, EqualityHelper.NotEqualsCaseSensitive(left, right));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void RoleHelper_ParseRoles_EmptyInput_ReturnsNoRoles(string? value)
        => Assert.Empty(RoleHelper.ParseRoles(value));

    [Theory]
    [InlineData("[\"Customer\",\"Waiter\"]")]
    [InlineData("Customer, Waiter")]
    public void RoleHelper_ParseRoles_JsonOrCsv_ParsesRoles(string value)
        => Assert.Equal(["Customer", "Waiter"], RoleHelper.ParseRoles(value));

    [Theory]
    [InlineData("[not json]")]
    [InlineData("[]")]
    public void RoleHelper_ParseRoles_InvalidOrEmptyJson_ReturnsEmptyList(string value)
        => Assert.Empty(RoleHelper.ParseRoles(value));

    [Fact]
    public void RoleHelper_DetermineActiveRole_PrefersWaiterOtherwiseCustomer()
    {
        Assert.Equal(Roles.Waiter.ToString(), RoleHelper.DetermineActiveRole([Roles.Customer.ToString(), Roles.Waiter.ToString()]));
        Assert.Equal(Roles.Customer.ToString(), RoleHelper.DetermineActiveRole([]));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("[\"Customer\"]", false)]
    [InlineData("[\"Waiter\"]", true)]
    public void RoleHelper_IsWaiter_UsesAuthorizerGroupClaim(string? groups, bool expected)
    {
        var request = RequestWithClaims(groups is null
            ? null
            : new Dictionary<string, string> { ["cognito:groups"] = groups });

        Assert.Equal(expected, RoleHelper.IsWaiter(request));
    }

    [Fact]
    public void RoleHelper_IsWaiter_MissingAuthorizer_ReturnsFalse()
        => Assert.False(RoleHelper.IsWaiter(new APIGatewayProxyRequest { RequestContext = new() }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("A")]
    [InlineData("A name")]
    public void PersonalDataValidator_ValidateName_InvalidValue_ThrowsValidationException(string? name)
        => Assert.Throws<ValidationException>(() => PersonalDataValidator.ValidateName(name!, "First name"));

    [Theory]
    [InlineData("Anne-Marie")]
    [InlineData("O'Neil")]
    public void PersonalDataValidator_ValidateName_AllowedName_DoesNotThrow(string name)
        => PersonalDataValidator.ValidateName(name, "First name");

    [Fact]
    public void PersonalDataValidator_ValidateName_MaximumLengthName_DoesNotThrow()
        => PersonalDataValidator.ValidateName(new string('a', 50), "First name");

    [Fact]
    public void PersonalDataValidator_ValidateName_NameOverMaximumLength_Throws()
        => Assert.Throws<ValidationException>(() => PersonalDataValidator.ValidateName(new string('a', 51), "Name"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("name@example.c")]
    public void PersonalDataValidator_ValidateEmail_InvalidEmail_ThrowsValidationException(string? email)
        => Assert.Throws<ValidationException>(() => PersonalDataValidator.ValidateEmail(email!));

    [Fact]
    public void PersonalDataValidator_ValidateEmail_TooLong_ThrowsValidationException()
        => Assert.Throws<ValidationException>(() => PersonalDataValidator.ValidateEmail($"{new string('a', 246)}@test.com"));

    [Theory]
    [InlineData("name@example.com")]
    [InlineData("first.last@my-domain.co.uk")]
    public void PersonalDataValidator_ValidateEmail_ValidEmail_DoesNotThrow(string email)
        => PersonalDataValidator.ValidateEmail(email);

    [Theory]
    [InlineData(null, "password")]
    [InlineData("short", "short")]
    [InlineData("12345678901234567", "12345678901234567")]
    [InlineData("password1", "password2")]
    public void PersonalDataValidator_ValidatePasswords_InvalidPair_ThrowsValidationException(
        string? password, string? confirmation)
        => Assert.Throws<ValidationException>(() => PersonalDataValidator.ValidatePasswords(password!, confirmation!));

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890123456")]
    public void PersonalDataValidator_ValidatePasswords_MatchingAllowedLength_DoesNotThrow(string password)
        => PersonalDataValidator.ValidatePasswords(password, password);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void PersonalDataValidator_ValidatePassword_MissingPassword_ThrowsValidationException(string? password)
        => Assert.Throws<ValidationException>(() => PersonalDataValidator.ValidatePassword(password));

    [Fact]
    public void PersonalDataValidator_ValidatePassword_NonEmptyPassword_DoesNotThrow()
        => PersonalDataValidator.ValidatePassword("password");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("10.5")]
    [InlineData("High")]
    public void OccupancyHelper_TryParseStrict_InvalidInput_ReturnsFalse(string? input)
    {
        Assert.False(OccupancyHelper.TryParseStrict(input, out var output));
        Assert.Equal(string.Empty, output);
    }

    [Theory]
    [InlineData("0", "0%")]
    [InlineData(" 65 ", "65%")]
    [InlineData("100%", "100%")]
    public void OccupancyHelper_TryParseStrict_ValidPercentValue_Normalizes(string input, string expected)
    {
        Assert.True(OccupancyHelper.TryParseStrict(input, out var output));
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData(null, "0%")]
    [InlineData("High", "75%")]
    [InlineData("medium", "50%")]
    [InlineData(" Low ", "25%")]
    [InlineData("legacy 140", "100%")]
    [InlineData("No value", "0%")]
    [InlineData("occupancy 42%", "42%")]
    public void OccupancyHelper_SanitizeLegacy_LegacyInput_ReturnsNormalizedPercent(string? input, string expected)
        => Assert.Equal(expected, OccupancyHelper.SanitizeLegacy(input));

    [Fact]
    public void UserContextHelper_GetAuthenticatedUserId_RequiresNonBlankSubjectClaim()
    {
        Assert.Equal("user-1", UserContextHelper.GetAuthenticatedUserId(RequestWithClaims(new() { ["sub"] = "user-1" })));
        Assert.Null(UserContextHelper.GetAuthenticatedUserId(RequestWithClaims(new() { ["sub"] = " " })));
        Assert.Null(UserContextHelper.GetAuthenticatedUserId(RequestWithClaims(null)));
        Assert.Null(UserContextHelper.GetAuthenticatedUserId(new APIGatewayProxyRequest { RequestContext = new() }));
    }

    [Theory]
    [InlineData("Customer", false)]
    [InlineData("Customer, Waiter", true)]
    [InlineData(null, false)]
    public void UserContextHelper_IsRequestFromWaiter_ParsesGroupClaim(string? groups, bool expected)
        => Assert.Equal(expected, UserContextHelper.IsRequestFromWaiter(RequestWithClaims(
            groups is null ? null : new() { ["cognito:groups"] = groups })));

    [Fact]
    public void RequestBodyHelper_TryParseAndValidate_ValidJson_ReturnsTypedValue()
    {
        var (context, logger) = CreateContext();
        var request = new APIGatewayProxyRequest { Body = """{"name":"Reservation","quantity":2}""" };

        var parsed = RequestBodyHelper.TryParseAndValidate<ValidatedRequest>(request, context.Object, out var value, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal("Reservation", value!.Name);
        Assert.Equal(2, value.Quantity);
        logger.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, "Body is empty")]
    [InlineData("", "Body is empty")]
    [InlineData("{", "Invalid json body")]
    [InlineData("null", "Body is empty")]
    public void RequestBodyHelper_TryParseAndValidate_EmptyMalformedOrNullJson_ReturnsBadRequest(
        string? body, string expectedError)
    {
        var (context, logger) = CreateContext();
        var request = new APIGatewayProxyRequest { Body = body };

        var parsed = RequestBodyHelper.TryParseAndValidate<ValidatedRequest>(request, context.Object, out var value, out var error);

        Assert.False(parsed);
        Assert.Null(value);
        Assert.Equal(400, error!.StatusCode);
        Assert.Contains(expectedError, error.Body);
        logger.Verify(logger => logger.LogLine(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void RequestBodyHelper_TryParseAndValidate_InvalidDataAnnotations_ReturnsValidationErrors()
    {
        var (context, _) = CreateContext();
        var request = new APIGatewayProxyRequest { Body = """{"name":"","quantity":0}""" };

        var parsed = RequestBodyHelper.TryParseAndValidate<ValidatedRequest>(request, context.Object, out var value, out var error);

        Assert.False(parsed);
        Assert.NotNull(value);
        Assert.Equal(400, error!.StatusCode);
        using var document = JsonDocument.Parse(error.Body);
        Assert.Contains("Name", document.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Contains("Quantity", document.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void DataAnnotationsValidator_TryValidate_ValidModel_ReturnsTrueWithoutResponse()
    {
        var valid = DataAnnotationsValidator.TryValidate(new ValidatedRequest { Name = "Valid", Quantity = 1 }, out var error);

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void DataAnnotationsValidator_TryValidate_InvalidModel_ReturnsStructured400Response()
    {
        var valid = DataAnnotationsValidator.TryValidate(new ValidatedRequest { Name = "", Quantity = 0 }, out var error);

        Assert.False(valid);
        Assert.Equal(400, error!.StatusCode);
        Assert.Contains("\"errors\"", error.Body);
    }

    private static APIGatewayProxyRequest RequestWithClaims(Dictionary<string, string>? claims)
        => new()
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = claims is null ? null : new APIGatewayCustomAuthorizerContext { Claims = claims }
            }
        };

    private static (Mock<ILambdaContext> Context, Mock<ILambdaLogger> Logger) CreateContext()
    {
        var context = new Mock<ILambdaContext>();
        var logger = new Mock<ILambdaLogger>();
        context.SetupGet(x => x.Logger).Returns(logger.Object);
        return (context, logger);
    }

    public sealed class ValidatedRequest
    {
        [Required]
        public string Name { get; set; } = "";

        [Range(1, 10)]
        public int Quantity { get; set; }
    }
}
