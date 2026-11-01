using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Handlers.Dishes;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers.Dishes;

[Collection("ResourceNamesEnvironment")]
public class GetDishesByLocationIdHandlerTests
{
    private readonly Mock<IDynamoDBContext> _db = new();
    private readonly Mock<ILambdaContext> _context = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public GetDishesByLocationIdHandlerTests()
    {
        _context.SetupGet(context => context.Logger).Returns(_logger.Object);
        Environment.SetEnvironmentVariable("locationsTable", "locations-for-tests");
        Environment.SetEnvironmentVariable("dishesTable", "dishes-for-tests");
    }

    [Fact]
    public async Task HandleRequest_MissingLocationId_ReturnsBadRequest()
    {
        var response = await Handle(null);

        Assert.Equal(400, response.StatusCode);
        _db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleRequest_LocationMissing_ReturnsNotFound()
    {
        _db.Setup(context => context.LoadAsync<Location>("missing", It.IsAny<LoadConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        var response = await Handle("missing");

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("missing", response.Body);
    }

    [Fact]
    public async Task HandleRequest_LocationHasNoSpecialtyDishes_ReturnsEmptyList()
    {
        _db.Setup(context => context.LoadAsync<Location>("location-1", It.IsAny<LoadConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Location("location-1", []));

        var response = await Handle("location-1");

        Assert.Equal(200, response.StatusCode);
        Assert.Empty(JsonDocument.Parse(response.Body).RootElement.EnumerateArray());
        _db.Verify(context => context.CreateBatchGet<Dish>(It.IsAny<DynamoDBOperationConfig>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_LocationHasSpecialtyDishes_ReturnsBatchResults()
    {
        var location = Location("location-1", ["dish-1"]);
        var dish = Dish("dish-1");
        var batch = new Mock<IBatchGet<Dish>>();
        batch.SetupGet(request => request.Results).Returns([dish]);
        batch.Setup(request => request.ExecuteAsync()).Returns(Task.CompletedTask);
        batch.Setup(request => request.AddKey("dish-1"));
        _db.Setup(context => context.LoadAsync<Location>("location-1", It.IsAny<LoadConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _db.Setup(context => context.CreateBatchGet<Dish>(It.IsAny<DynamoDBOperationConfig>()))
            .Returns(batch.Object);

        var response = await Handle("location-1");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("dish-1", JsonDocument.Parse(response.Body).RootElement[0].GetProperty("id").GetString());
        batch.Verify(request => request.AddKey("dish-1"), Times.Once);
        batch.Verify(request => request.ExecuteAsync(), Times.Once);
    }

    private Task<APIGatewayProxyResponse> Handle(string? locationId) =>
        new GetDishesByLocationIdHandler(_db.Object).HandleRequest(new APIGatewayProxyRequest
        {
            PathParameters = locationId is null ? null : new Dictionary<string, string> { ["id"] = locationId }
        }, _context.Object);

    private static Location Location(string id, List<string>? specialtyDishIds) => new()
    {
        Id = id,
        Address = "Central",
        Description = "Restaurant",
        AverageOccupancy = "50%",
        SpecialtyDishIds = specialtyDishIds
    };

    private static Dish Dish(string id) => new()
    {
        Id = id,
        Name = "Soup",
        Description = "Vegetable soup",
        Weight = "300g",
        Calories = "200",
        Proteins = "7g",
        Fats = "4g",
        Carbohydrates = "25g",
        Vitamins = "A",
        LocationId = "location-1"
    };
}
