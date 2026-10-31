using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Mappers;
using APIHandler.Models.Dishes;
using APIHandler.Models.Locations;

namespace APIHandler.Tests.Mappers;

public class ApiMapperTests
{
    [Fact]
    public void DishMapper_FromCreateRequest_CopiesValuesAndAssignsUniqueId()
    {
        var request = new CreateDishRequest
        {
            Name = "Soup",
            Description = "Seasonal soup",
            DishType = DishType.Appetizer,
            Price = 9.5m,
            Weight = "300g",
            Calories = "250",
            Proteins = "8g",
            Fats = "10g",
            Carbohydrates = "20g",
            Vitamins = "C",
            State = DishState.Available,
            LocationIds = ["loc-1"]
        };

        var first = DishMapper.FromCreateRequest(request);
        var second = DishMapper.FromCreateRequest(request);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(request.Name, first.Name);
        Assert.Equal(request.Description, first.Description);
        Assert.Equal(request.DishType, first.DishType);
        Assert.Equal(request.Price, first.Price);
        Assert.Equal(request.Weight, first.Weight);
        Assert.Equal(request.State, first.State);
    }

    [Theory]
    [InlineData(DishType.MainCourse, DishState.OnStop, "Main course", "On stop")]
    [InlineData(DishType.Dessert, DishState.OutOfStock, "Dessert", "Out of stock")]
    [InlineData((DishType)99, (DishState)99, "Unknown", "Unknown")]
    public void DishMapper_ToDishResponse_FormatsEnumLabelsAndOptionalImage(
        DishType dishType, DishState state, string expectedType, string expectedState)
    {
        var dish = CreateDish();
        dish.DishType = dishType;
        dish.State = state;
        dish.ImageUrl = "https://example.test/image.png";

        var response = DishMapper.ToDishResponse(dish);

        Assert.Equal(expectedType, response.DishType);
        Assert.Equal(expectedState, response.State);
        Assert.Equal(dish.ImageUrl, response.ImageUrl);
        Assert.Equal(dish.Name, response.Name);
        Assert.Equal(dish.Price, response.Price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DishMapper_ToDishListResponse_BlankImageUrlMapsToNull(string? imageUrl)
    {
        var dish = CreateDish();
        dish.ImageUrl = imageUrl;

        var response = DishMapper.ToDishListResponse(dish);

        Assert.Null(response.ImageUrl);
        Assert.Equal("Appetizer", response.DishType);
        Assert.Equal("Available", response.State);
    }

    [Fact]
    public void OrderMapper_ToOrderResponse_IncludesMatchingMenuDishesOnly()
    {
        var includedDish = CreateDish();
        var order = new Order
        {
            OrderId = "order-1",
            CustomerId = "customer-1",
            ReservationId = "reservation-1",
            ReservationAddress = "12 Main St",
            ReservationTime = "7:00 PM",
            Status = OrderStatus.Created,
            Dishes =
            [
                new OrderItem { DishId = includedDish.Id, Quantity = 2, State = OrderItemState.Pending },
                new OrderItem { DishId = "deleted-dish", Quantity = 1, State = OrderItemState.Pending }
            ]
        };

        var response = OrderMapper.ToOrderResponse(order, [includedDish]);

        Assert.Equal(order.OrderId, response.OrderId);
        Assert.Equal(order.ReservationId, response.ReservationId);
        var item = Assert.Single(response.OrderItems);
        Assert.Equal(includedDish.Id, item.Dish.Id);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void OrderMapper_ToOrderEntity_UsesRequestItemsAndCreatedStatus()
    {
        var request = new APIHandler.Models.Orders.CreateOrderRequest
        {
            ReservationId = "reservation-9",
            OrderItems = [new OrderItem { DishId = "dish-1", Quantity = 3 }]
        };

        var order = OrderMapper.ToOrderEntity(request, "customer-2", "Address", "Time");

        Assert.False(string.IsNullOrWhiteSpace(order.OrderId));
        Assert.Equal("customer-2", order.CustomerId);
        Assert.Equal("reservation-9", order.ReservationId);
        Assert.Equal("Address", order.ReservationAddress);
        Assert.Equal("Time", order.ReservationTime);
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Same(request.OrderItems, order.Dishes);
    }

    [Fact]
    public void OrderMapper_ToOrderEntity_NullItemsUsesEmptyList()
    {
        var request = new APIHandler.Models.Orders.CreateOrderRequest { ReservationId = "reservation" };
        request.OrderItems = null!;

        var order = OrderMapper.ToOrderEntity(request, "customer", "Address", "Time");

        Assert.Empty(order.Dishes);
    }

    [Fact]
    public void LocationMapper_FromCreateRequest_AssignsIdAndCopiesValues()
    {
        var request = new CreateLocationRequest
        {
            Address = "1 Main St",
            Description = "Downtown",
            TotalCapacity = 50,
            AverageOccupancy = "60%",
            Rating = 4.5
        };

        var location = LocationMapper.FromCreateRequest(request);

        Assert.False(string.IsNullOrWhiteSpace(location.Id));
        Assert.Equal(request.Address, location.Address);
        Assert.Equal(request.Description, location.Description);
        Assert.Equal(request.TotalCapacity, location.TotalCapacity);
        Assert.Equal(request.AverageOccupancy, location.AverageOccupancy);
        Assert.Equal(request.Rating, location.Rating);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void LocationMapper_ToLocationResponse_BlankImageMapsToNull(string? imageUrl)
    {
        var response = LocationMapper.ToLocationResponse(new Location
        {
            Id = "loc",
            Address = "1 Main St",
            Description = "Cafe",
            AverageOccupancy = "20%",
            ImageUrl = imageUrl,
            TotalCapacity = 20,
            Rating = 4
        });

        Assert.Null(response.ImageUrl);
        Assert.Equal("loc", response.Id);
        Assert.Equal("20%", response.AverageOccupancy);
    }

    private static Dish CreateDish() => new()
    {
        Id = "dish-1",
        Name = "Soup",
        Description = "Seasonal soup",
        DishType = DishType.Appetizer,
        Price = 9.5m,
        Weight = "300g",
        Calories = "250",
        Proteins = "8g",
        Fats = "10g",
        Carbohydrates = "20g",
        Vitamins = "C",
        State = DishState.Available
    };
}
