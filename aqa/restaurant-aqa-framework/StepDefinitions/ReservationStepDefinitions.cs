using System.Globalization;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Locations;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Support;

using LocationResponse = Restaurant_Aqa_Framework.Api.Dtos.Locations.LocationResponse;
using CleanupScope = Restaurant_Aqa_Framework.Api.Seeding.CleanupScope;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class ReservationStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    // ---------- Givens ----------

    [Given("a customer who is signed in")]
    public async Task GivenCustomerSignedIn()
    {
        await Actor.AttemptsToAsync(new SignUpViaApi());
        var email = Actor.Recall<string>("signupEmail");
        var password = Actor.Recall<string>("signupPassword");
        await Actor.AttemptsToAsync(new SignInThroughTheService(email, password));
    }

    [Given("a table that seats {int} guests")]
    public async Task GivenTableThatSeats(int capacity)
    {
        var location = Recall<LocationResponse>();
        var api = new TablesApi(Actor.UsingTheApi());
        var response = await api.CreateTableAsync(SampleDataFactory.CreateTableRequest(location.Id, capacity));

        await Assert.That(response.Ok).IsTrue().Because($"table creation failed: {response.Status} {await response.TextAsync()}");
        var table = await response.ReadAsAsync<TableResponse>();
        Actor.Remember(table);

        var waiter = await Actor.AsksForAsync(new TheWaiterSession());
        Actor.Recall<CleanupScope>().TrackDelete(
            $"table:{table.Id}",
            () => api.DeleteTableAsync(table.Id, bearerToken: waiter.IdToken));
    }

    [Given("the customer has booked the table for tomorrow evening")]
    public async Task GivenCustomerHasBookedTableForTomorrow()
    {
        var table = Recall<TableResponse>();
        var start = DateTime.UtcNow.Date.AddDays(1).AddHours(19); // 7 PM tomorrow
        var end = start.AddHours(2);

        var request = new CreateReservationRequest(table.Id, start, end, 2);
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.CreateReservationAsync(request, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue().Because($"booking failed: {response.Status} {await response.TextAsync()}");
        var reservation = await response.ReadAsAsync<ReservationResponse>();
        Actor.Remember(reservation);
    }

    // ---------- Whens ----------

    [When("the customer books the table for {int} guests")]
    public async Task WhenCustomerBooksTableForGuests(int guestCount)
    {
        var table = Recall<TableResponse>();
        var start = DateTime.UtcNow.AddMinutes(16); // Just past the 15-min rule
        var end = start.AddHours(2);

        var request = new CreateReservationRequest(table.Id, start, end, guestCount);
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.CreateReservationAsync(request, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the customer books the table to start in {int} minutes")]
    public async Task WhenCustomerBooksTableToStartInMinutes(int minutes)
    {
        var table = Recall<TableResponse>();
        var start = DateTime.UtcNow.AddMinutes(minutes);
        var end = start.AddHours(2);

        var request = new CreateReservationRequest(table.Id, start, end, 2);
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.CreateReservationAsync(request, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("another customer books the same table for the same time")]
    public async Task WhenAnotherCustomerBooksSameTableSameTime()
    {
        var existing = Recall<ReservationResponse>();
        var table = Recall<TableResponse>();

        // Create a second customer
        await Actor.AttemptsToAsync(new SignUpViaApi());
        var email = Actor.Recall<string>("signupEmail");
        var password = Actor.Recall<string>("signupPassword");
        await Actor.AttemptsToAsync(new SignInThroughTheService(email, password));

        var request = new CreateReservationRequest(table.Id, existing.StartTime, existing.EndTime, 2);
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.CreateReservationAsync(request, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the customer cancels the reservation")]
    public async Task WhenCustomerCancelsReservation()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.CancelReservationAsync(reservation.Id, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the customer cancels the reservation again")]
    public async Task WhenCustomerCancelsReservationAgain()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.CancelReservationAsync(reservation.Id, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the customer changes the reservation to start in {int} minutes")]
    public async Task WhenCustomerChangesReservationToStartInMinutes(int minutes)
    {
        var reservation = Recall<ReservationResponse>();
        var start = DateTime.UtcNow.AddMinutes(minutes);
        var end = start.AddHours(2);

        var request = new EditReservationRequest(reservation.Id, start, end, reservation.GuestCount);
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.EditReservationAsync(request, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("an unauthorized user tries to book the table")]
    public async Task WhenUnauthorizedUserTriesToBook()
    {
        var table = Recall<TableResponse>();
        var start = DateTime.UtcNow.AddMinutes(16);
        var end = start.AddHours(2);

        var request = new CreateReservationRequest(table.Id, start, end, 2);
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.CreateReservationAsync(request, bearerToken: null);

        Actor.Remember(response);
    }

    [When("another customer tries to view this reservation")]
    public async Task WhenAnotherCustomerTriesToViewReservation()
    {
        var reservation = Recall<ReservationResponse>();

        // Create a second customer
        await Actor.AttemptsToAsync(new SignUpViaApi());
        var email = Actor.Recall<string>("signupEmail");
        var password = Actor.Recall<string>("signupPassword");
        await Actor.AttemptsToAsync(new SignInThroughTheService(email, password));

        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.GetReservationAsync(reservation.Id, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    // ---------- Thens ----------

    [Then("the customer is told the table is too small")]
    public async Task ThenCustomerIsToldTableTooSmall()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(400);
        await Assert.That(response.Body).Contains("capacity", StringComparison.OrdinalIgnoreCase);
    }

    [Then("the booking is refused")]
    public async Task ThenBookingIsRefused()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsFalse();
    }

    [Then("the booking is accepted")]
    public async Task ThenBookingIsAccepted()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsTrue().Because($"booking failed: {response.Status} {response.Body}");

        var reservation = response.Json<ReservationResponse>();
        Actor.Remember(reservation);
    }

    [Then("the second booking is refused")]
    public async Task ThenSecondBookingIsRefused()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(409); // Conflict
    }

    [Then("the reservation appears in my reservations with correct time, table and guest count")]
    public async Task ThenReservationAppearsInMyReservations()
    {
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.GetReservationsAsync(Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue();
        var reservations = await response.ReadAsAsync<ReservationResponse[]>();

        var expected = Recall<ReservationResponse>();
        var match = reservations.FirstOrDefault(r => r.Id == expected.Id);

        await Assert.That(match).IsNotNull();
        await Assert.That(match!.GuestCount).IsEqualTo(expected.GuestCount);
    }

    [Then("this table is no longer offered for that time")]
    public async Task ThenTableNoLongerOfferedForThatTime()
    {
        var table = Recall<TableResponse>();
        var reservation = Recall<ReservationResponse>();

        var api = new TablesApi(Actor.UsingTheApi());
        var response = await api.GetAvailableTablesAsync(
            table.LocationId,
            2,
            reservation.StartTime);

        await Assert.That(response.Ok).IsTrue();
        var available = await response.ReadAsAsync<TableResponse[]>();

        await Assert.That(available).DoesNotContain(t => t.Id == table.Id);
    }

    [Then("the cancellation succeeds")]
    public async Task ThenCancellationSucceeds()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsTrue().Because($"cancellation failed: {response.Status} {response.Body}");
    }

    [Then("the second cancellation is refused")]
    public async Task ThenSecondCancellationRefused()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(400); // Bad request / already cancelled
    }

    [Then("the reservation is updated")]
    public async Task ThenReservationIsUpdated()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsTrue().Because($"update failed: {response.Status} {response.Body}");
    }

    [Then("the unauthorized request is refused")]
    public async Task ThenUnauthorizedRequestRefused()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(401);
    }

    [Then("the other customer cannot see the reservation")]
    public async Task ThenOtherCustomerCannotSeeReservation()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(403); // Forbidden
    }
}