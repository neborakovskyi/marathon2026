using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers;
using APIHandler.Records;
using APIHandler.Routing;
using Moq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace APIHandler.Tests.Handlers;

public class FunctionDispatcherTests
{
    [Fact]
    public async Task FunctionHandler_MatchingRoute_NormalizesMethodAndTrailingSlashAndPassesPathValues()
    {
        var (function, handler, context, _) = CreateFunction(
            "GET", "/locations/{locationId}", new APIGatewayProxyResponse { StatusCode = 204 });
        var request = new APIGatewayProxyRequest { HttpMethod = "get", Path = "/locations/loc-4/" };

        var response = await function.FunctionHandler(request, context.Object);

        Assert.Equal(204, response.StatusCode);
        Assert.Equal("loc-4", request.PathParameters!["locationId"]);
        handler.Verify(h => h.HandleRequest(request, context.Object), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_OptionsRequest_ReturnsCorsPreflightWithoutCallingRoute()
    {
        var (function, handler, context, logger) = CreateFunction(
            "OPTIONS", "/ignored", new APIGatewayProxyResponse { StatusCode = 204 });

        var response = await function.FunctionHandler(
            new APIGatewayProxyRequest { HttpMethod = "OPTIONS", Path = "/anything" },
            context.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Body);
        Assert.Equal("*", response.Headers["Access-Control-Allow-Origin"]);
        handler.VerifyNoOtherCalls();
        logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FunctionHandler_UnmatchedPathOrMethod_ReturnsNotFound()
    {
        var (function, handler, context, _) = CreateFunction(
            "GET", "/known", new APIGatewayProxyResponse { StatusCode = 200 });

        var wrongPath = await function.FunctionHandler(
            new APIGatewayProxyRequest { HttpMethod = "GET", Path = "/unknown" },
            context.Object);
        var wrongMethod = await function.FunctionHandler(
            new APIGatewayProxyRequest { HttpMethod = "POST", Path = "/known" },
            context.Object);

        Assert.Equal(404, wrongPath.StatusCode);
        Assert.Equal(404, wrongMethod.StatusCode);
        handler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FunctionHandler_HandlerThrows_ReturnsGenericInternalError()
    {
        var (function, handler, context, logger) = CreateFunction(
            "GET", "/failure", new APIGatewayProxyResponse());
        handler.Setup(h => h.HandleRequest(It.IsAny<APIGatewayProxyRequest>(), It.IsAny<ILambdaContext>()))
            .ThrowsAsync(new InvalidOperationException("private implementation detail"));

        var response = await function.FunctionHandler(
            new APIGatewayProxyRequest { HttpMethod = "GET", Path = "/failure" },
            context.Object);

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("Internal Server Error", response.Body);
        Assert.DoesNotContain("private implementation detail", response.Body);
        logger.Verify(l => l.LogError(It.Is<string>(message => message.Contains("InvalidOperationException"))), Times.Once);
    }

    private static (
        APIHandler.Function Function,
        Mock<IApiRequestHandler> Handler,
        Mock<ILambdaContext> Context,
        Mock<ILambdaLogger> Logger) CreateFunction(
        string method, string template, APIGatewayProxyResponse response)
    {
        // The production constructor builds every AWS client through RouteRegistry.
        // Populate only the dispatcher table so these tests never instantiate or call AWS clients.
        var function = (APIHandler.Function)RuntimeHelpers.GetUninitializedObject(typeof(APIHandler.Function));
        var handler = new Mock<IApiRequestHandler>();
        handler.Setup(h => h.HandleRequest(It.IsAny<APIGatewayProxyRequest>(), It.IsAny<ILambdaContext>()))
            .ReturnsAsync(response);
        var routeHandlers = new List<RouteHandler>
        {
            new(new Route(method, template), handler.Object)
        };
        typeof(APIHandler.Function)
            .GetField("_routeTable", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(function, new Dictionary<string, List<RouteHandler>>
            {
                [method.ToUpperInvariant()] = routeHandlers
            });

        var logger = new Mock<ILambdaLogger>();
        var context = new Mock<ILambdaContext>();
        context.SetupGet(c => c.Logger).Returns(logger.Object);
        return (function, handler, context, logger);
    }
}
