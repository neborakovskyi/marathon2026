using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers;
using APIHandler.Helpers;
using APIHandler.Records;
using APIHandler.Routing;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Helpers;

public class ResponseAndRoutingTests
{
    private readonly Mock<ILambdaLogger> _logger = new();

    [Fact]
    public void ResponseBuilder_CreateErrorResponse_SerializesMessageAndLogsError()
    {
        var exception = new InvalidOperationException("backend detail");

        var response = ResponseBuilder.CreateErrorResponse(409, "Conflict", _logger.Object, exception);

        Assert.Equal(409, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal("Conflict", body.RootElement.GetProperty("error").GetString());
        Assert.Equal("application/json", response.Headers["Content-Type"]);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        _logger.Verify(logger => logger.LogLine("[ERROR] Conflict"), Times.Once);
        _logger.Verify(logger => logger.LogError(It.Is<string>(s => s.Contains("InvalidOperationException"))), Times.Once);
    }

    [Theory]
    [InlineData(typeof(GroupReservationExpiredException), "GroupReservationExpired")]
    [InlineData(typeof(GuestLimitExceededException), "GuestLimitExceeded")]
    [InlineData(typeof(GroupReservationSubmittedException), "GroupReservationSubmitted")]
    [InlineData(typeof(GuestSessionExpiredException), "GuestSessionExpired")]
    [InlineData(typeof(ReservationNotFoundException), "Unknown")]
    public void ResponseBuilder_CreateErrorCodeResponse_MapsDomainException(Type type, string expectedCode)
    {
        var exception = CreateValidationException(type);

        var response = ResponseBuilder.CreateErrorCodeResponse(exception);

        Assert.Equal(400, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal(exception.Message, body.RootElement.GetProperty("message").GetString());
        Assert.Equal(expectedCode, body.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public void ResponseBuilder_CreateSuccessAndOptionsResponses_ReturnExpectedHeadersAndBody()
    {
        var message = ResponseBuilder.CreateSuccessResponse("done");
        var options = ResponseBuilder.CreateOptionsResponse();
        var objectResponse = ResponseBuilder.CreateSuccessResponse(new { MessageText = "value" }, 201);

        Assert.Equal(200, message.StatusCode);
        Assert.Contains("\"message\":\"done\"", message.Body);
        Assert.Equal(200, options.StatusCode);
        Assert.Null(options.Body);
        Assert.Equal(message.Headers, options.Headers);
        Assert.Equal(201, objectResponse.StatusCode);
        Assert.Contains("\"MessageText\":\"value\"", objectResponse.Body);
    }

    [Fact]
    public void ResponseBuilder_CreateJsonResponse_UsesCamelCaseAndRelaxedUnicodeEncoding()
    {
        var response = ResponseBuilder.CreateJsonResponse(202, new { FirstName = "Zoë & Co" });

        Assert.Equal(202, response.StatusCode);
        Assert.Contains("\"firstName\":\"Zoë & Co\"", response.Body);
    }

    [Fact]
    public void ResponseBuilder_CreatePlainTextResponse_UsesTextContentTypeAndCors()
    {
        var response = ResponseBuilder.CreatePlainTextResponse(206, "csv body");

        Assert.Equal(206, response.StatusCode);
        Assert.Equal("csv body", response.Body);
        Assert.Equal("text/plain", response.Headers["Content-Type"]);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        Assert.Equal(2, response.Headers.Count);
    }

    [Theory]
    [InlineData("text/csv", false)]
    [InlineData("application/json", false)]
    [InlineData("application/pdf", true)]
    public void ResponseBuilder_CreateFileResponse_TextIsUtf8AndBinaryIsBase64(
        string contentType, bool expectedBase64)
    {
        byte[] content = [0, 1, 2, 255];

        var response = ResponseBuilder.CreateFileResponse(content, "report.data", contentType);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(expectedBase64, response.IsBase64Encoded);
        Assert.Equal(contentType, response.Headers["Content-Type"]);
        Assert.Equal("attachment; filename=\"report.data\"", response.Headers["Content-Disposition"]);
        Assert.Equal(expectedBase64 ? Convert.ToBase64String(content) : System.Text.Encoding.UTF8.GetString(content), response.Body);
    }

    [Fact]
    public void RouteHandler_Match_ExtractsNamedPathValuesAndRejectsOtherPaths()
    {
        var handler = new RouteHandler(new Route("GET", "/locations/{locationId}/dishes/{dishId}"), Mock.Of<IApiRequestHandler>());

        var match = handler.Match("/locations/loc-7/dishes/dish-2");

        Assert.NotNull(match);
        Assert.Equal("loc-7", match["locationId"]);
        Assert.Equal("dish-2", match["dishId"]);
        Assert.Null(handler.Match("/locations/loc-7/dishes"));
        Assert.Null(handler.Match("/locations/loc-7/dishes/dish-2/extra"));
    }

    [Fact]
    public void RouteHandler_Match_OnlyMatchesSingleSegmentForParameter()
    {
        var handler = new RouteHandler(new Route("GET", "/users/{id}/name"), Mock.Of<IApiRequestHandler>());

        Assert.Null(handler.Match("/users/a/b/name"));
        Assert.NotNull(handler.Match("/users/u-123/name"));
    }

    private static ValidationException CreateValidationException(Type type)
    {
        if (type == typeof(GroupReservationSubmittedException))
            return new GroupReservationSubmittedException();
        if (type == typeof(ReservationNotFoundException))
            return new ReservationNotFoundException("ignored by specialized exception");
        if (type == typeof(GuestSessionExpiredException))
            return new GuestSessionExpiredException("validation failed");
        return (ValidationException)Activator.CreateInstance(type, "validation failed")!;
    }
}
