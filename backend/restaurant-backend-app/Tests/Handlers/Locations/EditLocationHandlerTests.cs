using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Handlers.Locations;
using APIHandler.Models.Locations;
using APIHandler.Services;
using APIHandler.Services.Locations;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Locations;

[Collection("ResourceNamesEnvironment")]
public class EditLocationHandlerTests
{
    private readonly Mock<ILocationService> _locations = new();
    private readonly Mock<IAmazonS3> _s3 = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public EditLocationHandlerTests()
    {
        _context.SetupGet(context => context.Logger).Returns(_logger.Object);
        Environment.SetEnvironmentVariable("imagesBucketName", "images-for-tests");
        Environment.SetEnvironmentVariable("REGION", "us-west-2");
    }

    [Fact]
    public async Task HandleRequest_MissingLocationId_ReturnsBadRequest()
    {
        var response = await Handle(null, Body("65%"));

        Assert.Equal(400, response.StatusCode);
        _locations.VerifyNoOtherCalls();
        _s3.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task HandleRequest_EmptyBody_ReturnsBadRequest(string? body)
    {
        var response = await Handle("location-1", body);

        Assert.Equal(400, response.StatusCode);
        _locations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task HandleRequest_InvalidPayload_ReturnsBadRequest(string body)
    {
        var response = await Handle("location-1", body);

        Assert.Equal(400, response.StatusCode);
        _locations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("high")]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("65.5")]
    public async Task HandleRequest_InvalidOccupancy_ReturnsBadRequest(string occupancy)
    {
        var response = await Handle("location-1", Body(occupancy));

        Assert.Equal(400, response.StatusCode);
        _locations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_ValidUpdate_NormalizesOccupancyAndReturnsLocation()
    {
        _locations.Setup(service => service.UpdateLocationAsync("location-1",
                It.Is<CreateLocationRequest>(request => request.AverageOccupancy == "65%"),
                null))
            .ReturnsAsync(Location());

        var response = await Handle("location-1", Body(" 065 "));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Central", JsonDocument.Parse(response.Body).RootElement.GetProperty("address").GetString());
        Assert.Equal("65%", JsonDocument.Parse(response.Body).RootElement.GetProperty("averageOccupancy").GetString());
    }

    [Fact]
    public async Task HandleRequest_ServiceValidationFailure_ReturnsBadRequest()
    {
        _locations.Setup(service => service.UpdateLocationAsync("location-1",
                It.IsAny<CreateLocationRequest>(), null))
            .ThrowsAsync(new ValidationException("Location is unavailable."));

        var response = await Handle("location-1", Body("65"));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Location is unavailable", response.Body);
    }

    [Fact]
    public async Task HandleRequest_UnexpectedServiceFailure_ReturnsInternalServerError()
    {
        _locations.Setup(service => service.UpdateLocationAsync("location-1",
                It.IsAny<CreateLocationRequest>(), null))
            .ThrowsAsync(new InvalidOperationException("test failure"));

        var response = await Handle("location-1", Body("65"));

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("internal error", response.Body);
        _logger.Verify(logger => logger.LogLine(It.Is<string>(message => message.Contains("test failure"))), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_ImageProvided_UploadsAndPassesImageUrlToService()
    {
        _s3.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());
        string? receivedImageUrl = null;
        _locations.Setup(service => service.UpdateLocationAsync("location-1",
                It.IsAny<CreateLocationRequest>(), It.IsAny<string?>()))
            .Callback<string, CreateLocationRequest, string?>((_, _, imageUrl) => receivedImageUrl = imageUrl)
            .ReturnsAsync(Location());

        var response = await Handle("location-1", Body("65", Convert.ToBase64String(CreatePng())));

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("assets/img/locations/location_location-1", receivedImageUrl);
        _s3.Verify(client => client.PutObjectAsync(It.Is<PutObjectRequest>(request =>
            request.BucketName == "images-for-tests" && request.ContentType == "image/png"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private Task<APIGatewayProxyResponse> Handle(string? locationId, string? body) =>
        new EditLocationHandler(_locations.Object, new S3ImageService(_s3.Object))
            .HandleRequest(new APIGatewayProxyRequest
            {
                Body = body,
                PathParameters = locationId is null ? null : new Dictionary<string, string> { ["id"] = locationId }
            }, _context.Object);

    private static string Body(string occupancy, string? imageBase64 = null) => JsonSerializer.Serialize(new
    {
        address = "Central",
        description = "Restaurant",
        totalCapacity = 24,
        averageOccupancy = occupancy,
        rating = 4.5,
        imageBase64
    });

    private static Location Location() => new()
    {
        Id = "location-1",
        Address = "Central",
        Description = "Restaurant",
        TotalCapacity = 24,
        AverageOccupancy = "65%"
    };

    private static byte[] CreatePng()
    {
        using var image = Image.LoadPixelData<Rgba32>([new Rgba32(12, 34, 56, 255)], 1, 1);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
