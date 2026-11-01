using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using APIHandler.Entities;
using APIHandler.Repositories.Dishes;
using Moq;

namespace APIHandler.Tests.Repositories;

[Collection("ResourceNamesEnvironment")]
public class DishRepositoryTests
{
    private readonly Mock<IDynamoDBContext> _db = new();
    private readonly DishRepository _repository;

    public DishRepositoryTests()
    {
        Environment.SetEnvironmentVariable("dishesTable", "dishes-for-tests");
        Environment.SetEnvironmentVariable("locationsTable", "locations-for-tests");
        _repository = new DishRepository(_db.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetDishesByLocationIdAsync_EmptyLocationId_ReturnsAllDishesWithoutLocationLookup(string? locationId)
    {
        var allDishes = new[] { Dish("dish-1", "location-1") };
        var search = Search(allDishes);
        _db.Setup(context => context.ScanAsync<Dish>(It.IsAny<IEnumerable<ScanCondition>>(),
                It.IsAny<ScanConfig>()))
            .Returns(search.Object);

        var result = await _repository.GetDishesByLocationIdAsync(locationId!);

        Assert.Equal(allDishes, result);
        _db.Verify(context => context.LoadAsync<Location>(It.IsAny<object>(),
            It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _db.Verify(context => context.ScanAsync<Dish>(It.IsAny<IEnumerable<ScanCondition>>(),
            It.IsAny<ScanConfig>()), Times.Once);
    }

    [Fact]
    public async Task GetDishesByLocationIdAsync_LocationHasNoSpecialties_ReturnsDirectDishes()
    {
        var location = Location("location-1", []);
        var directDishes = new[] { Dish("dish-1", "location-1") };
        var directSearch = Search(directDishes);
        _db.Setup(context => context.LoadAsync<Location>("location-1", It.IsAny<LoadConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _db.Setup(context => context.ScanAsync<Dish>(It.IsAny<IEnumerable<ScanCondition>>(),
                It.IsAny<ScanConfig>()))
            .Returns(directSearch.Object);

        var result = await _repository.GetDishesByLocationIdAsync("location-1");

        Assert.Equal(directDishes, result);
        _db.Verify(context => context.ScanAsync<Dish>(It.IsAny<IEnumerable<ScanCondition>>(),
            It.IsAny<ScanConfig>()), Times.Once);
    }

    [Fact]
    public async Task GetDishesByLocationIdAsync_MergesSpecialtyAndDirectDishesWithoutDuplicates()
    {
        var location = Location("location-1", ["specialty-1"]);
        var specialty = Dish("specialty-1", "other-location");
        var batch = new Mock<IBatchGet<Dish>>();
        batch.SetupGet(request => request.Results).Returns([specialty]);
        batch.Setup(request => request.ExecuteAsync()).Returns(Task.CompletedTask);
        batch.Setup(request => request.AddKey("specialty-1"));
        _db.Setup(context => context.LoadAsync<Location>("location-1", It.IsAny<LoadConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _db.Setup(context => context.CreateBatchGet<Dish>(It.IsAny<BatchGetConfig>())).Returns(batch.Object);
        var directSearch = Search([specialty, Dish("direct-1", "location-1")]);
        _db.Setup(context => context.ScanAsync<Dish>(It.IsAny<IEnumerable<ScanCondition>>(),
                It.IsAny<ScanConfig>()))
            .Returns(directSearch.Object);

        var result = (await _repository.GetDishesByLocationIdAsync("location-1")).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, dish => dish.Id == "specialty-1");
        Assert.Contains(result, dish => dish.Id == "direct-1");
        batch.Verify(request => request.AddKey("specialty-1"), Times.Once);
        batch.Verify(request => request.ExecuteAsync(), Times.Once);
    }

    [Fact]
    public async Task GetDishesByLocationIdAsync_NoMatchingDishes_FallsBackToAllDishes()
    {
        var location = Location("location-1", []);
        var search = Search(Array.Empty<Dish>());
        var allDishes = new[] { Dish("dish-2", "location-2") };
        var allSearch = Search(allDishes);
        _db.Setup(context => context.LoadAsync<Location>("location-1", It.IsAny<LoadConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _db.SetupSequence(context => context.ScanAsync<Dish>(It.IsAny<IEnumerable<ScanCondition>>(),
                It.IsAny<ScanConfig>()))
            .Returns(search.Object)
            .Returns(allSearch.Object);

        var result = await _repository.GetDishesByLocationIdAsync("location-1");

        Assert.Equal(allDishes, result);
        _db.Verify(context => context.ScanAsync<Dish>(It.IsAny<IEnumerable<ScanCondition>>(),
            It.IsAny<ScanConfig>()), Times.Exactly(2));
    }

    private static Mock<IAsyncSearch<T>> Search<T>(IEnumerable<T> results)
    {
        var search = new Mock<IAsyncSearch<T>>();
        search.Setup(request => request.GetRemainingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(results.ToList());
        return search;
    }

    private static Location Location(string id, List<string>? specialtyDishIds) => new()
    {
        Id = id,
        Address = "Central",
        Description = "Restaurant",
        AverageOccupancy = "50%",
        SpecialtyDishIds = specialtyDishIds
    };

    private static Dish Dish(string id, string locationId) => new()
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
        LocationId = locationId
    };
}
