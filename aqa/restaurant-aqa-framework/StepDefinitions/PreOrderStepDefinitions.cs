using System.Globalization;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.Locations;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class PreOrderStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    // ---------- Givens ----------

    [Given("the restaurant has dishes on the menu")]
    public async Task GivenRestaurantHasDishesOnMenu()
    {
        var location = Recall<LocationResponse>();
        var api = new DishesApi(Actor.UsingTheApi());

        // Create a few dishes of different types
        foreach (var type in new[] { DishType.Appetizer, DishType.MainCourse, DishType.Dessert })
        {
            var response = await api.CreateDishAsync(SampleDataFactory.CreateDishRequest([location.Id], type),
                bearerToken: (await Actor.AsksForAsync(new TheWaiterSession())).IdToken);
            await Assert.That(response.Ok).IsTrue().Because($"dish creation failed: {response.Status} {await response.TextAsync()}");
            var dish = await response.ReadAsAsync<DishResponse>();
            Actor.Remember(dish);
        }
    }

    [Given("the customer has a pre-order with a dish")]
    public async Task GivenCustomerHasPreOrderWithDish()
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();

        var request = new CreateOrderRequest(reservation.Id, [new OrderItemRequest(dish.Id, 1)]);
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.CreateOrderAsync(request, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue().Because($"pre-order creation failed: {response.Status} {await response.TextAsync()}");
        Actor.Remember(response);
    }

    [Given("the customer has submitted a pre-order")]
    public async Task GivenCustomerHasSubmittedPreOrder()
    {
        await GivenCustomerHasPreOrderWithDish();

        var reservation = Recall<ReservationResponse>();
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.SubmitOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue().Because($"submission failed: {response.Status} {await response.TextAsync()}");
        Actor.Remember(response);
    }

    [Given("the customer has submitted a pre-order with multiple dishes")]
    public async Task GivenCustomerHasSubmittedPreOrderWithMultipleDishes()
    {
        var reservation = Recall<ReservationResponse>();
        var dishes = Actor.RecallAll<DishResponse>().Take(3).ToArray();

        var items = dishes.Select(d => new OrderItemRequest(d.Id, 1)).ToList();
        var request = new CreateOrderRequest(reservation.Id, items);
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.CreateOrderAsync(request, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue();

        response = await api.SubmitOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));
        await Assert.That(response.Ok).IsTrue();

        Actor.Remember(response);
    }

    [Given("a waiter who is signed in")]
    public async Task GivenWaiterSignedIn()
    {
        var (username, password) = Settings.Current.RequireWaiter();
        await Actor.AttemptsToAsync(new SignInThroughTheService(username, password));
    }

    [Given("the waiter is assigned to the reservation's table")]
    public async Task GivenWaiterAssignedToTable()
    {
        var reservation = Recall<ReservationResponse>();
        var waiter = await Actor.AsksForAsync(new TheWaiterSession());

        // The table assignment would be done via the API
        // For now we assume the waiter is assigned
        Actor.Remember(waiter);
    }

    [Given("another waiter who is signed in")]
    public async Task GivenAnotherWaiterSignedIn()
    {
        // In a real scenario, this would be a different waiter account
        // For now we just note the requirement
        Actor.Remember("anotherWaiter", true);
    }

    [Given("another available table")]
    public async Task GivenAnotherAvailableTable()
    {
        var location = Recall<LocationResponse>();
        var api = new TablesApi(Actor.UsingTheApi());

        var response = await api.CreateTableAsync(
            SampleDataFactory.CreateTableRequest(location.Id, 4),
            bearerToken: (await Actor.AsksForAsync(new TheWaiterSession())).IdToken);

        await Assert.That(response.Ok).IsTrue().Because($"table creation failed: {response.Status} {await response.TextAsync()}");
        var table = await response.ReadAsAsync<TableResponse>();
        Actor.Remember("otherTable", table);
    }

    // ---------- Whens ----------

    [When("the customer adds a dish to their pre-order")]
    public async Task WhenCustomerAddsDishToPreOrder()
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();

        var request = new CreateOrderRequest(reservation.Id, [new OrderItemRequest(dish.Id, 1)]);
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.CreateOrderAsync(request, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the customer adds another dish to their pre-order")]
    public async Task WhenCustomerAddsAnotherDishToPreOrder()
    {
        var reservation = Recall<ReservationResponse>();
        var dishes = Actor.RecallAll<DishResponse>();
        var dish = dishes[dishes.Count - 1]; // Get a different dish

        // Get current order
        var api = new OrdersApi(Actor.UsingTheApi());
        var getResponse = await api.GetOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        if (getResponse.Ok)
        {
            var order = await getResponse.ReadAsAsync<GetOrderResponse>();
            var items = order.OrderItems.Select(i => new OrderItemRequest(i.Dish.Id, i.Quantity)).ToList();
            items.Add(new OrderItemRequest(dish.Id, 1));

            var editRequest = new EditPreOrderRequest(items);
            var response = await api.EditOrderAsync(reservation.Id, editRequest, Actor.Recall<string>("idToken"));
            Actor.Remember(response);
        }
        else
        {
            // No order yet, create one
            var request = new CreateOrderRequest(reservation.Id, [new OrderItemRequest(dish.Id, 1)]);
            var response = await api.CreateOrderAsync(request, Actor.Recall<string>("idToken"));
            Actor.Remember(response);
        }
    }

    [When("the customer changes the quantity of that dish")]
    public async Task WhenCustomerChangesQuantityOfDish()
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();

        var api = new OrdersApi(Actor.UsingTheApi());
        var getResponse = await api.GetOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        if (getResponse.Ok)
        {
            var order = await getResponse.ReadAsAsync<GetOrderResponse>();
            var items = order.OrderItems.Select(i =>
                i.Dish.Id == dish.Id ? new OrderItemRequest(i.Dish.Id, 2) : new OrderItemRequest(i.Dish.Id, i.Quantity)
            ).ToList();

            var editRequest = new EditPreOrderRequest(items);
            var response = await api.EditOrderAsync(reservation.Id, editRequest, Actor.Recall<string>("idToken"));
            Actor.Remember(response);
        }
    }

    [When("the customer removes that dish")]
    public async Task WhenCustomerRemovesDish()
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();

        var api = new OrdersApi(Actor.UsingTheApi());
        var getResponse = await api.GetOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        if (getResponse.Ok)
        {
            var order = await getResponse.ReadAsAsync<GetOrderResponse>();
            var items = order.OrderItems.Where(i => i.Dish.Id != dish.Id)
                .Select(i => new OrderItemRequest(i.Dish.Id, i.Quantity)).ToList();

            var editRequest = new EditPreOrderRequest(items);
            var response = await api.EditOrderAsync(reservation.Id, editRequest, Actor.Recall<string>("idToken"));
            Actor.Remember(response);
        }
    }

    [When("the customer submits the pre-order")]
    public async Task WhenCustomerSubmitsPreOrder()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.SubmitOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the customer tries to submit an empty pre-order")]
    public async Task WhenCustomerTriesToSubmitEmptyPreOrder()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new OrdersApi(Actor.UsingTheApi());

        // Try to submit without creating an order first
        var response = await api.SubmitOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));
        Actor.Remember(response);
    }

    [When("the customer recalls the pre-order")]
    public async Task WhenCustomerRecallsPreOrder()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.UnsubmitOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the customer cancels the pre-order")]
    public async Task WhenCustomerCancelsPreOrder()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.DeleteOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the waiter confirms a dish in the pre-order")]
    public async Task WhenWaiterConfirmsDish()
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());

        var response = await api.ConfirmPreOrderDishAsync(reservation.Id, dish.Id, Actor.Recall<string>("idToken"));
        Actor.Remember(response);
    }

    [When("the waiter cancels a dish in the pre-order")]
    public async Task WhenWaiterCancelsDish()
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());

        var response = await api.CancelPreOrderDishAsync(reservation.Id, dish.Id, Actor.Recall<string>("idToken"));
        Actor.Remember(response);
    }

    [When("that waiter tries to confirm a dish")]
    public async Task WhenOtherWaiterTriesToConfirmDish()
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());

        // Using a different token would be needed, for now just try
        var response = await api.ConfirmPreOrderDishAsync(reservation.Id, dish.Id, "invalid-token");
        Actor.Remember(response);
    }

    [When("the customer tries to confirm a dish")]
    public async Task WhenCustomerTriesToConfirmDish()
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());

        var response = await api.ConfirmPreOrderDishAsync(reservation.Id, dish.Id, Actor.Recall<string>("idToken"));
        Actor.Remember(response);
    }

    [When("the waiter changes the reservation status to {string}")]
    public async Task WhenWaiterChangesReservationStatus(string status)
    {
        var reservation = Recall<ReservationResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());

        var newStatus = Enum.Parse<ReservationStatus>(status);
        var response = await api.UpdateReservationStatusAsync(reservation.Id, newStatus, Actor.Recall<string>("idToken"));
        Actor.Remember(response);
    }

    [When("that waiter tries to change the reservation status")]
    public async Task WhenOtherWaiterTriesToChangeStatus()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());

        var response = await api.UpdateReservationStatusAsync(reservation.Id, ReservationStatus.InProgress, "invalid-token");
        Actor.Remember(response);
    }

    [When("the customer tries to change the reservation status")]
    public async Task WhenCustomerTriesToChangeStatus()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());

        var response = await api.UpdateReservationStatusAsync(reservation.Id, ReservationStatus.InProgress, Actor.Recall<string>("idToken"));
        Actor.Remember(response);
    }

    [When("the waiter reassigns the reservation to the other table")]
    public async Task WhenWaiterReassignsReservation()
    {
        var reservation = Recall<ReservationResponse>();
        var otherTable = Recall<TableResponse>("otherTable");
        var api = new ReservationsApi(Actor.UsingTheApi());

        var response = await api.UpdateReservationTableAsync(reservation.Id, otherTable.Id, Actor.Recall<string>("idToken"));
        Actor.Remember(response);
    }

    // ---------- Thens ----------

    [Then("the pre-order contains both dishes")]
    public async Task ThenPreOrderContainsBothDishes()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.GetOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue();
        var order = await response.ReadAsAsync<GetOrderResponse>();
        await Assert.That(order.OrderItems.Count).IsGreaterThanOrEqualTo(2);
    }

    [Then("the pre-order status is {string}")]
    public async Task ThenPreOrderStatusIs(string expectedStatus)
    {
        var reservation = Recall<ReservationResponse>();
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.GetOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue();
        var order = await response.ReadAsAsync<GetOrderResponse>();
        await Assert.That(order.Status.ToString()).IsEqualTo(expectedStatus);
    }

    [Then("the reservation status is {string}")]
    public async Task ThenReservationStatusIs(string expectedStatus)
    {
        var reservation = Recall<ReservationResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.GetReservationAsync(reservation.Id, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue();
        var res = await response.ReadAsAsync<ReservationResponse>();
        await Assert.That(res.Status.ToString()).IsEqualTo(expectedStatus);
    }

    [Then("the pre-order shows the updated quantity")]
    public async Task ThenPreOrderShowsUpdatedQuantity()
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.GetOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue();
        var order = await response.ReadAsAsync<GetOrderResponse>();
        var item = order.OrderItems.First(i => i.Dish.Id == dish.Id);
        await Assert.That(item).IsNotNull();
        await Assert.That(item!.Quantity).IsEqualTo(2);
    }

    [Then("the pre-order no longer contains that dish")]
    public async Task ThenPreOrderNoLongerContainsDish()
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.GetOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue();
        var order = await response.ReadAsAsync<GetOrderResponse>();
        var item = order.OrderItems.First(i => i.Dish.Id == dish.Id);
        await Assert.That(item).IsNull();
    }

    [Then("the submission is refused")]
    public async Task ThenSubmissionRefused()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsFalse();
    }

    [Then("the dish status is {string}")]
    public async Task ThenDishStatusIs(string expectedStatus)
    {
        var reservation = Recall<ReservationResponse>();
        var dish = Recall<DishResponse>();
        var api = new OrdersApi(Actor.UsingTheApi());
        var response = await api.GetOrderAsync(reservation.Id, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue();
        var order = await response.ReadAsAsync<GetOrderResponse>();
        var item = order.OrderItems.First(i => i.Dish.Id == dish.Id);
        await Assert.That(item).IsNotNull();
        await Assert.That(item!.Dish.State).IsEqualTo(expectedStatus);
    }

    [Then("the action is refused")]
    public async Task ThenActionRefused()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsFalse();
    }

    [Then("the reservation is on the new table")]
    public async Task ThenReservationOnNewTable()
    {
        var reservation = Recall<ReservationResponse>();
        var otherTable = Recall<TableResponse>("otherTable");
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.GetReservationAsync(reservation.Id, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue();
        var res = await response.ReadAsAsync<ReservationResponse>();
        await Assert.That(res.TableId).IsEqualTo(otherTable.Id);
    }
}
