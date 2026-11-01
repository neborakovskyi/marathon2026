using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Handlers.Dishes;
using APIHandler.Models.Dishes;
using APIHandler.Services;
using APIHandler.Services.Dishes;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Dishes;

[Collection("ResourceNamesEnvironment")]
public class EditDishHandlerTests
{
    private readonly Mock<IDishService> _dishService = new();
    private readonly Mock<IAmazonS3> _s3 = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public EditDishHandlerTests()
    {
        _context.SetupGet(c => c.Logger).Returns(_logger.Object);
        Environment.SetEnvironmentVariable("imagesBucketName", "images-for-tests");
        Environment.SetEnvironmentVariable("REGION", "us-west-2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task HandleRequest_EmptyBody_ReturnsBadRequest(string? body)
    {
        var response = await Handle("dish-1", body);

        Assert.Equal(400, response.StatusCode);
        _dishService.VerifyNoOtherCalls();
        _s3.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task HandleRequest_InvalidPayload_ReturnsBadRequest(string body)
    {
        var response = await Handle("dish-1", body);

        Assert.Equal(400, response.StatusCode);
        _dishService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_MissingDishId_ReturnsBadRequest()
    {
        var response = await new EditDishHandler(_dishService.Object, new S3ImageService(_s3.Object))
            .HandleRequest(new APIGatewayProxyRequest { Body = Body() }, _context.Object);

        Assert.Equal(400, response.StatusCode);
        _dishService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_ValidUpdate_ReturnsUpdatedDish()
    {
        var dish = Dish("dish-1");
        _dishService.Setup(service => service.UpdateDishAsync("dish-1", It.IsAny<CreateDishRequest>(), null))
            .ReturnsAsync(dish);

        var response = await Handle("dish-1", Body());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Soup", JsonDocument.Parse(response.Body).RootElement.GetProperty("name").GetString());
        _dishService.Verify(service => service.UpdateDishAsync("dish-1",
            It.Is<CreateDishRequest>(request => request.Name == "Soup"), null), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_ServiceValidationFailure_ReturnsBadRequest()
    {
        _dishService.Setup(service => service.UpdateDishAsync("missing", It.IsAny<CreateDishRequest>(), null))
            .ThrowsAsync(new ValidationException("Dish not found."));

        var response = await Handle("missing", Body());

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Dish not found", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ImageProvided_UploadsAndPassesPublicUrlToService()
    {
        _s3.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());
        string? receivedUrl = null;
        _dishService.Setup(service => service.UpdateDishAsync("dish-1", It.IsAny<CreateDishRequest>(), It.IsAny<string?>()))
            .Callback<string, CreateDishRequest, string?>((_, _, imageUrl) => receivedUrl = imageUrl)
            .ReturnsAsync(Dish("dish-1"));

        var base64 = Convert.ToBase64String(CreatePng());
        var response = await Handle("dish-1", Body(base64));

        Assert.Equal(200, response.StatusCode);
        Assert.StartsWith("https://images-for-tests.s3.", receivedUrl);
        Assert.Contains(".amazonaws.com/assets/img/dishes/dish_dish-1", receivedUrl);
        _s3.Verify(client => client.PutObjectAsync(It.Is<PutObjectRequest>(request =>
            request.BucketName == "images-for-tests" && request.ContentType == "image/png"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private Task<APIGatewayProxyResponse> Handle(string? dishId, string? body) =>
        new EditDishHandler(_dishService.Object, new S3ImageService(_s3.Object))
            .HandleRequest(new APIGatewayProxyRequest
            {
                Body = body,
                PathParameters = dishId is null ? [] : new Dictionary<string, string> { ["id"] = dishId }
            }, _context.Object);

    private static string Body(string? imageBase64 = null) => JsonSerializer.Serialize(new
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
        locationIds = Array.Empty<string>(),
        imageBase64
    });

    private static Dish Dish(string id) => new()
    {
        Id = id,
        Name = "Soup",
        Description = "Fresh vegetables",
        DishType = DishType.Appetizer,
        Price = 12.5m,
        Weight = "350g",
        Calories = "250",
        Proteins = "8g",
        Fats = "5g",
        Carbohydrates = "30g",
        Vitamins = "A",
        State = DishState.Available
    };

    private static byte[] CreatePng()
    {
        using var image = Image.LoadPixelData<Rgba32>([new Rgba32(12, 34, 56, 255)], 1, 1);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
