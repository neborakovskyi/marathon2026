using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.Dishes;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.Locations;
using APIHandler.Services.Dishes;
using Moq;

namespace APIHandler.Tests.Services;

public class DishServiceTests
{
    private readonly Mock<IDishRepository> _dishes = new();
    private readonly Mock<ILocationRepository> _locations = new();
    private readonly DishService _service;

    public DishServiceTests() => _service = new DishService(_dishes.Object, _locations.Object);

    [Fact]
    public async Task GetDishByIdAsync_DishExists_ReturnsDish()
    {
        var dish = ExistingDish();
        _dishes.Setup(repository => repository.GetDishByIdAsync(dish.Id)).ReturnsAsync(dish);

        var result = await _service.GetDishByIdAsync(dish.Id);

        Assert.Same(dish, result);
    }

    [Fact]
    public async Task GetDishByIdAsync_DishMissing_ThrowsValidationException()
    {
        _dishes.Setup(repository => repository.GetDishByIdAsync("missing")).ReturnsAsync((Dish?)null);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.GetDishByIdAsync("missing"));

        Assert.Contains("missing", exception.Message);
    }

    [Fact]
    public async Task UpdateDishAsync_UpdatesDishAndAddsItToMatchingLocations()
    {
        var dish = ExistingDish();
        var firstLocation = Location("location-1", null);
        var unrelatedLocation = Location("location-2", ["other-dish"]);
        _dishes.Setup(repository => repository.GetDishByIdAsync(dish.Id)).ReturnsAsync(dish);
        _dishes.Setup(repository => repository.SaveDishAsync(dish)).Returns(Task.CompletedTask);
        _locations.Setup(repository => repository.GetAllLocationsAsync())
            .ReturnsAsync(new[] { firstLocation, unrelatedLocation });
        _locations.Setup(repository => repository.SaveLocationAsync(firstLocation)).Returns(Task.CompletedTask);

        var result = await _service.UpdateDishAsync(dish.Id, UpdateRequest(["location-1"]), "https://image/new.png");

        Assert.Same(dish, result);
        Assert.Equal("Updated soup", dish.Name);
        Assert.Equal(14m, dish.Price);
        Assert.Equal("https://image/new.png", dish.ImageUrl);
        Assert.Equal(["dish-1"], firstLocation.SpecialtyDishIds);
        _locations.Verify(repository => repository.SaveLocationAsync(firstLocation), Times.Once);
        _locations.Verify(repository => repository.SaveLocationAsync(unrelatedLocation), Times.Never);
    }

    [Fact]
    public async Task UpdateDishAsync_ExistingDishAlreadyLinked_DoesNotDuplicateLocationLink()
    {
        var dish = ExistingDish();
        var location = Location("location-1", ["dish-1"]);
        _dishes.Setup(repository => repository.GetDishByIdAsync(dish.Id)).ReturnsAsync(dish);
        _dishes.Setup(repository => repository.SaveDishAsync(dish)).Returns(Task.CompletedTask);
        _locations.Setup(repository => repository.GetAllLocationsAsync()).ReturnsAsync([location]);

        await _service.UpdateDishAsync(dish.Id, UpdateRequest(["location-1"]), null);

        Assert.Equal(["dish-1"], location.SpecialtyDishIds);
        Assert.Equal("https://old-image/image.png", dish.ImageUrl);
        _locations.Verify(repository => repository.SaveLocationAsync(It.IsAny<Location>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDishAsync_DishMissing_ThrowsValidationException()
    {
        _dishes.Setup(repository => repository.GetDishByIdAsync("missing")).ReturnsAsync((Dish?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateDishAsync("missing", UpdateRequest([]), null));

        _dishes.Verify(repository => repository.SaveDishAsync(It.IsAny<Dish>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDishAsync_LinkedDishWithoutForce_RejectsDeletion()
    {
        var dish = ExistingDish();
        _dishes.Setup(repository => repository.GetDishByIdAsync(dish.Id)).ReturnsAsync(dish);
        _locations.Setup(repository => repository.GetAllLocationsAsync())
            .ReturnsAsync([Location("location-1", ["dish-1"])]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.DeleteDishAsync(dish.Id));

        Assert.Contains("currently on the menu", exception.Message);
        _dishes.Verify(repository => repository.DeleteDishAsync(It.IsAny<Dish>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDishAsync_ForceRemovesDishFromLocationsAndDeletesDish()
    {
        var dish = ExistingDish();
        var location = Location("location-1", ["dish-1", "other-dish"]);
        _dishes.Setup(repository => repository.GetDishByIdAsync(dish.Id)).ReturnsAsync(dish);
        _locations.Setup(repository => repository.GetAllLocationsAsync()).ReturnsAsync([location]);
        _locations.Setup(repository => repository.SaveLocationAsync(location)).Returns(Task.CompletedTask);
        _dishes.Setup(repository => repository.DeleteDishAsync(dish)).Returns(Task.CompletedTask);

        await _service.DeleteDishAsync(dish.Id, force: true);

        Assert.Equal(["other-dish"], location.SpecialtyDishIds);
        _locations.Verify(repository => repository.SaveLocationAsync(location), Times.Once);
        _dishes.Verify(repository => repository.DeleteDishAsync(dish), Times.Once);
    }

    [Fact]
    public async Task DeleteDishAsync_DishMissing_ThrowsValidationException()
    {
        _dishes.Setup(repository => repository.GetDishByIdAsync("missing")).ReturnsAsync((Dish?)null);

        await Assert.ThrowsAsync<ValidationException>(() => _service.DeleteDishAsync("missing"));

        _locations.Verify(repository => repository.GetAllLocationsAsync(), Times.Never);
    }

    private static Dish ExistingDish() => new()
    {
        Id = "dish-1",
        Name = "Soup",
        Description = "Original",
        DishType = DishType.Appetizer,
        Price = 10m,
        Weight = "300g",
        Calories = "200",
        Proteins = "7g",
        Fats = "4g",
        Carbohydrates = "25g",
        Vitamins = "A",
        ImageUrl = "https://old-image/image.png",
        State = DishState.Available,
        LocationId = "location-1"
    };

    private static Location Location(string id, List<string>? specialtyDishIds) => new()
    {
        Id = id,
        Address = "Central",
        Description = "Restaurant",
        AverageOccupancy = "50%",
        SpecialtyDishIds = specialtyDishIds
    };

    private static CreateDishRequest UpdateRequest(List<string>? locationIds) => new()
    {
        Name = "Updated soup",
        Description = "Updated description",
        DishType = DishType.MainCourse,
        Price = 14m,
        Weight = "350g",
        Calories = "250",
        Proteins = "8g",
        Fats = "5g",
        Carbohydrates = "30g",
        Vitamins = "B",
        State = DishState.Available,
        LocationIds = locationIds!
    };
}
