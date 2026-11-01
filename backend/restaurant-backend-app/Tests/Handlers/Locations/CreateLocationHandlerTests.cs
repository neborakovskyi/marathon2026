using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Handlers.Locations;
using APIHandler.Models.Locations;
using APIHandler.Services;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Locations;

[Collection("ResourceNamesEnvironment")]
public class CreateLocationHandlerTests
{
    private readonly Mock<IDynamoDBContext> _db = new();
    private readonly Mock<Amazon.S3.IAmazonS3> _s3 = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public CreateLocationHandlerTests()
    {
        _context.SetupGet(c => c.Logger).Returns(_logger.Object);
        Environment.SetEnvironmentVariable("locationsTable", "locations-for-tests");
        Environment.SetEnvironmentVariable("imagesBucketName", "images-for-tests");
        Environment.SetEnvironmentVariable("REGION", "us-west-2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task HandleRequest_EmptyBody_ReturnsBadRequest(string? body)
    {
        var response = await Handle(body);

        Assert.Equal(400, response.StatusCode);
        _db.VerifyNoOtherCalls();
        _s3.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task HandleRequest_InvalidPayload_ReturnsBadRequest(string body)
    {
        var response = await Handle(body);

        Assert.Equal(400, response.StatusCode);
        _db.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("high")]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("65.5")]
    public async Task HandleRequest_InvalidOccupancy_ReturnsBadRequest(string occupancy)
    {
        var response = await Handle(Body(occupancy));

        Assert.Equal(400, response.StatusCode);
        _db.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("65", "65%")]
    [InlineData(" 065% ", "65%")]
    [InlineData("0", "0%")]
    [InlineData("100%", "100%")]
    public async Task HandleRequest_ValidOccupancy_CreatesLocationWithNormalizedPercent(
        string occupancy, string expectedOccupancy)
    {
        Location? saved = null;
        _db.Setup(db => db.SaveAsync(It.IsAny<Location>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Callback<Location, SaveConfig, CancellationToken>((location, _, _) => saved = location)
            .Returns(Task.CompletedTask);

        var response = await Handle(Body(occupancy));

        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(saved);
        Assert.Equal(expectedOccupancy, saved.AverageOccupancy);
        Assert.Equal("Central", saved.Address);
        Assert.Equal(24, saved.TotalCapacity);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal(expectedOccupancy, body.RootElement.GetProperty("averageOccupancy").GetString());
    }

    private Task<APIGatewayProxyResponse> Handle(string? body) =>
        new CreateLocationHandler(_db.Object, new S3ImageService(_s3.Object))
            .HandleRequest(new APIGatewayProxyRequest { Body = body }, _context.Object);

    private static string Body(string occupancy) => JsonSerializer.Serialize(new CreateLocationRequest
    {
        Address = "Central",
        Description = "Restaurant",
        TotalCapacity = 24,
        AverageOccupancy = occupancy,
        Rating = 4.5
    });
}
