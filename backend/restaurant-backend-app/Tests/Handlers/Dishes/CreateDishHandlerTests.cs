using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using APIHandler.Entities;
using APIHandler.Handlers.Dishes;
using APIHandler.Services;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Dishes;

[Collection("ResourceNamesEnvironment")]
public class CreateDishHandlerTests
{
    private readonly Mock<IDynamoDBContext> _db = new();
    private readonly Mock<IAmazonS3> _s3 = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public CreateDishHandlerTests()
    {
        _context.SetupGet(c => c.Logger).Returns(_logger.Object);
        Environment.SetEnvironmentVariable("locationsTable", "locations-for-tests");
        Environment.SetEnvironmentVariable("dishesTable", "dishes-for-tests");
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
    public async Task HandleRequest_InvalidJsonOrNullPayload_ReturnsBadRequest(string body)
    {
        var response = await Handle(body);

        Assert.Equal(400, response.StatusCode);
        _db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_ValidDishAndLocations_SavesDishAndAddsDishToEachLocation()
    {
        var location1 = Location("location-1", ["existing-dish"]);
        var location2 = Location("location-2", null);
        SetupLocation(location1);
        SetupLocation(location2);
        Dish? savedDish = null;
        var savedLocations = new List<Location>();
        _db.Setup(db => db.SaveAsync(It.IsAny<Dish>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Callback<Dish, SaveConfig, CancellationToken>((dish, _, _) => savedDish = dish)
            .Returns(Task.CompletedTask);
        _db.Setup(db => db.SaveAsync(It.IsAny<Location>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Callback<Location, SaveConfig, CancellationToken>((location, _, _) => savedLocations.Add(location))
            .Returns(Task.CompletedTask);

        var response = await Handle(Body(locationIds: ["location-1", "location-2"]));

        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(savedDish);
        Assert.Equal("Soup", savedDish.Name);
        Assert.Equal("Fresh vegetables", savedDish.Description);
        Assert.Equal(12.5m, savedDish.Price);
        Assert.Equal("Available", JsonDocument.Parse(response.Body).RootElement.GetProperty("state").GetString());
        Assert.Equal("existing-dish", location1.SpecialtyDishIds![0]);
        Assert.Contains(savedDish.Id, location1.SpecialtyDishIds);
        Assert.Equal([savedDish.Id], location2.SpecialtyDishIds);
        Assert.Equal(2, savedLocations.Count);
    }

    [Fact]
    public async Task HandleRequest_LocationMissing_ReturnsNotFoundAfterSavingDish()
    {
        _db.Setup(db => db.SaveAsync(It.IsAny<Dish>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _db.Setup(db => db.LoadAsync<Location>("missing-location", It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        var response = await Handle(Body(locationIds: ["missing-location"]));

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("missing-location", response.Body);
        _db.Verify(db => db.SaveAsync(It.IsAny<Dish>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        _db.Verify(db => db.SaveAsync(It.IsAny<Location>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_LocationWithExistingSpecialtyDish_PreservesExistingAndAddsNewDish()
    {
        var location = Location("location-1", ["existing-dish"]);
        SetupLocation(location);
        _db.Setup(db => db.SaveAsync(It.IsAny<Dish>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await Handle(Body(locationIds: ["location-1"]));

        Assert.Equal(201, response.StatusCode);
        Assert.Equal(2, location.SpecialtyDishIds!.Count);
        Assert.Equal("existing-dish", location.SpecialtyDishIds[0]);
        _db.Verify(db => db.SaveAsync(location, It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_ImageProvided_UploadsToS3AndReturnsPublicImageUrl()
    {
        var location = Location("location-1", []);
        SetupLocation(location);
        _db.Setup(db => db.SaveAsync(It.IsAny<Dish>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _db.Setup(db => db.SaveAsync(It.IsAny<Location>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        PutObjectRequest? upload = null;
        _s3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => upload = request)
            .ReturnsAsync(new PutObjectResponse());
        var png = CreatePng();

        var response = await Handle(Body(["location-1"], Convert.ToBase64String(png)));

        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(upload);
        Assert.Equal("images-for-tests", upload.BucketName);
        Assert.StartsWith("assets/img/dishes/dish_", upload.Key);
        Assert.Equal("image/png", upload.ContentType);
        var imageUrl = JsonDocument.Parse(response.Body).RootElement.GetProperty("imageUrl").GetString();
        Assert.Equal($"https://images-for-tests.s3.us-west-2.amazonaws.com/{upload.Key}", imageUrl);
    }

    [Fact]
    public async Task HandleRequest_NoLocations_CreatesDishWithoutLocationLookups()
    {
        _db.Setup(db => db.SaveAsync(It.IsAny<Dish>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await Handle(Body(locationIds: []));

        Assert.Equal(201, response.StatusCode);
        _db.Verify(db => db.SaveAsync(It.IsAny<Dish>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        _db.Verify(db => db.LoadAsync<Location>(It.IsAny<object>(), It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupLocation(Location location) =>
        _db.Setup(db => db.LoadAsync<Location>(location.Id, It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

    private Task<APIGatewayProxyResponse> Handle(string? body) =>
        new CreateDishHandler(_db.Object, new S3ImageService(_s3.Object))
            .HandleRequest(new APIGatewayProxyRequest { Body = body }, _context.Object);

    private static string Body(List<string>? locationIds = null, string? imageBase64 = null) =>
        JsonSerializer.Serialize(new
        {
            name = "Soup",
            description = "Fresh vegetables",
            dishType = "Appetizer",
            price = 12.5m,
            weight = "350g",
            calories = "250",
            proteins = "8g",
            fats = "5g",
            carbohydrates = "30g",
            vitamins = "A",
            state = "Available",
            locationIds = locationIds ?? ["location-1"],
            imageBase64
        });

    private static Location Location(string id, List<string>? dishes) => new()
    {
        Id = id,
        Address = "Central",
        Description = "Restaurant",
        AverageOccupancy = "medium",
        SpecialtyDishIds = dishes
    };

    private static byte[] CreatePng()
    {
        using var image = Image.LoadPixelData<Rgba32>([new Rgba32(12, 34, 56, 255)], 1, 1);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
