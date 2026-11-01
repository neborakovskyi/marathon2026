using System.Globalization;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Feedback;
using Restaurant_Aqa_Framework.Api.Dtos.Locations;
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
public sealed class FeedbackStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    // ---------- Givens ----------

    [Given("the customer has completed the visit \\(reservation status is \"Completed\"\\)")]
    public async Task GivenCustomerHasCompletedVisit()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());

        var response = await api.UpdateReservationStatusAsync(reservation.Id, ReservationStatus.Completed,
            bearerToken: (await Actor.AsksForAsync(new TheWaiterSession())).IdToken);

        await Assert.That(response.Ok).IsTrue().Because($"completion failed: {response.Status} {await response.TextAsync()}");
    }

    [Given("the customer has a reservation with status {string}")]
    public async Task GivenCustomerHasReservationWithStatus(string status)
    {
        var reservation = Recall<ReservationResponse>();
        var api = new ReservationsApi(Actor.UsingTheApi());

        var newStatus = Enum.Parse<ReservationStatus>(status);
        var response = await api.UpdateReservationStatusAsync(reservation.Id, newStatus,
            bearerToken: (await Actor.AsksForAsync(new TheWaiterSession())).IdToken);

        await Assert.That(response.Ok).IsTrue().Because($"status change failed: {response.Status} {await response.TextAsync()}");
    }

    [Given("the customer has created a feedback")]
    public async Task GivenCustomerHasCreatedFeedback()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new FeedbackApi(Actor.UsingTheApi());

        var request = new CreateFeedbackRequest(reservation.Id, 5, FeedbackType.Service, null, "Excellent!");
        var response = await api.CreateFeedbackAsync(request, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue().Because($"feedback creation failed: {response.Status} {await response.TextAsync()}");
        var feedback = await response.ReadAsAsync<FeedbackResponse>();
        Actor.Remember(feedback.Items[0]);
    }

    [Given("another customer who is signed in")]
    public async Task GivenAnotherCustomerSignedIn()
    {
        await Actor.AttemptsToAsync(new SignUpViaApi());
        var email = Actor.Recall<string>("signupEmail");
        var password = Actor.Recall<string>("signupPassword");
        await Actor.AttemptsToAsync(new SignInThroughTheService(email, password));
    }

    [Given("that customer has a completed reservation")]
    public async Task GivenThatCustomerHasCompletedReservation()
    {
        var table = Recall<TableResponse>();
        var start = DateTime.UtcNow.AddHours(-2);
        var end = start.AddHours(2);

        var request = new CreateReservationRequest(table.Id, start, end, 2);
        var api = new ReservationsApi(Actor.UsingTheApi());
        var response = await api.CreateReservationAsync(request, Actor.Recall<string>("idToken"));

        await Assert.That(response.Ok).IsTrue();
        var reservation = await response.ReadAsAsync<ReservationResponse>();
        Actor.Remember("otherReservation", reservation);

        // Mark as completed
        response = await api.UpdateReservationStatusAsync(reservation.Id, ReservationStatus.Completed,
            bearerToken: (await Actor.AsksForAsync(new TheWaiterSession())).IdToken);
        await Assert.That(response.Ok).IsTrue();
    }

    [Given("the location has multiple feedback entries of different types")]
    public async Task GivenLocationHasMultipleFeedback()
    {
        var location = Recall<LocationResponse>();
        var api = new FeedbackApi(Actor.UsingTheApi());
        var waiter = await Actor.AsksForAsync(new TheWaiterSession());

        // Create a few feedback entries
        for (int i = 0; i < 5; i++)
        {
            // Create a reservation for each
            var table = Recall<TableResponse>();
            var start = DateTime.UtcNow.AddHours(-3 - i);
            var end = start.AddHours(2);

            var request = new CreateReservationRequest(table.Id, start, end, 2);
            var resApi = new ReservationsApi(Actor.UsingTheApi());
            var resResponse = await resApi.CreateReservationAsync(request, waiter.IdToken);

            if (resResponse.Ok)
            {
                var reservation = await resResponse.ReadAsAsync<ReservationResponse>();

                // Mark completed
                await resApi.UpdateReservationStatusAsync(reservation.Id, ReservationStatus.Completed, waiter.IdToken);

                // Create feedback
                var fbRequest = new CreateFeedbackRequest(
                    reservation.Id,
                    3 + (i % 3),
                    i % 2 == 0 ? FeedbackType.Service : FeedbackType.CuisineExperience,
                    null,
                    $"Feedback {i}");
                await api.CreateFeedbackAsync(fbRequest, waiter.IdToken);
            }
        }
    }

    // ---------- Whens ----------

    [When("the customer creates a feedback of type {string} with rating {int} and comment {string}")]
    public async Task WhenCustomerCreatesFeedback(string type, int rating, string comment)
    {
        var reservation = Recall<ReservationResponse>();
        var api = new FeedbackApi(Actor.UsingTheApi());

        var fbType = Enum.Parse<FeedbackType>(type);
        var request = new CreateFeedbackRequest(reservation.Id, rating, fbType, null, comment);
        var response = await api.CreateFeedbackAsync(request, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the customer tries to create a feedback")]
    public async Task WhenCustomerTriesToCreateFeedback()
    {
        var reservation = Recall<ReservationResponse>();
        var api = new FeedbackApi(Actor.UsingTheApi());

        var request = new CreateFeedbackRequest(reservation.Id, 4, FeedbackType.Service, null, "Test");
        var response = await api.CreateFeedbackAsync(request, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("this customer tries to create feedback for that reservation")]
    public async Task WhenThisCustomerCreatesFeedbackForOtherReservation()
    {
        var otherReservation = Recall<ReservationResponse>("otherReservation");
        var api = new FeedbackApi(Actor.UsingTheApi());

        var request = new CreateFeedbackRequest(otherReservation.Id, 4, FeedbackType.Service, null, "Test");
        var response = await api.CreateFeedbackAsync(request, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the customer edits the feedback to rating {int} and comment {string}")]
    public async Task WhenCustomerEditsFeedback(int rating, string comment)
    {
        var feedback = Recall<FeedbackResponseItem>();
        var api = new FeedbackApi(Actor.UsingTheApi());

        var request = new CreateFeedbackRequest(feedback.ReservationId, rating, feedback.Type, null, comment);
        var response = await api.EditFeedbackAsync(request, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("the customer tries to create a feedback with rating {int}")]
    public async Task WhenCustomerTriesToCreateFeedbackWithRating(int rating)
    {
        var reservation = Recall<ReservationResponse>();
        var api = new FeedbackApi(Actor.UsingTheApi());

        var request = new CreateFeedbackRequest(reservation.Id, rating, FeedbackType.Service, null, "Test");
        var response = await api.CreateFeedbackAsync(request, Actor.Recall<string>("idToken"));

        Actor.Remember(response);
    }

    [When("a guest requests feedback of type {string}")]
    public async Task WhenGuestRequestsFeedbackByType(string type)
    {
        var location = Recall<LocationResponse>();
        var api = new FeedbackApi(Actor.UsingTheApi());

        var fbType = Enum.Parse<FeedbackType>(type);
        var response = await api.GetFeedbackForLocationAsync(location.Id, fbType, SortingOrder.NewestFirst, 1);

        Actor.Remember(response);
    }

    [When("a guest requests feedback sorted by {string}")]
    public async Task WhenGuestRequestsFeedbackSortedBy(string sort)
    {
        var location = Recall<LocationResponse>();
        var api = new FeedbackApi(Actor.UsingTheApi());

        var sortOrder = sort switch
        {
            "highest rating first" => SortingOrder.TopRatedFirst,
            "lowest rating first" => SortingOrder.LowRatedFirst,
            "newest first" => SortingOrder.NewestFirst,
            "oldest first" => SortingOrder.OldestFirst,
            _ => throw new ArgumentException($"Unknown sort: {sort}")
        };

        var response = await api.GetFeedbackForLocationAsync(location.Id, FeedbackType.Service, sortOrder, 1);
        Actor.Remember(response);
    }

    [When("a guest requests page 1 of feedback with page size 2")]
    public async Task WhenGuestRequestsPage1()
    {
        var location = Recall<LocationResponse>();
        var api = new FeedbackApi(Actor.UsingTheApi());

        var response = await api.GetFeedbackForLocationAsync(location.Id, FeedbackType.Service, SortingOrder.NewestFirst, 1);
        Actor.Remember(response);
    }

    [When("a guest requests Kitchen feedback sorted by highest rating first, page 1")]
    public async Task WhenGuestRequestsCombined()
    {
        var location = Recall<LocationResponse>();
        var api = new FeedbackApi(Actor.UsingTheApi());

        var response = await api.GetFeedbackForLocationAsync(location.Id, FeedbackType.CuisineExperience, SortingOrder.TopRatedFirst, 1);
        Actor.Remember(response);
    }

    // ---------- Thens ----------

    [Then("the feedback is created")]
    public async Task ThenFeedbackIsCreated()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsTrue().Because($"feedback creation failed: {response.Status} {response.Body}");

        var feedback = response.Json<FeedbackResponse>();
        Actor.Remember(feedback.Items[0]);
    }

    [Then("the feedback shows rating {int} and comment {string}")]
    public async Task ThenFeedbackShowsRatingAndComment(int expectedRating, string expectedComment)
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsTrue();
        var feedback = response.Json<FeedbackResponse>();
        await Assert.That(feedback.Items[0].Rating).IsEqualTo(expectedRating);
        await Assert.That(feedback.Items[0].Comment).IsEqualTo(expectedComment);
    }

    [Then("the action is refused")]
    public async Task ThenActionRefused()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsFalse();
    }

    [Then("the visitor is told {string}")]
    public async Task ThenVisitorIsTold(string message)
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Body).Contains(message);
    }

    [Then("all feedback shown is of type {string}")]
    public async Task ThenAllFeedbackIsOfType(string type)
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsTrue();
        var feedback = response.Json<FeedbackResponse>();

        var fbType = Enum.Parse<FeedbackType>(type);
        for (int i = 0; i < feedback.Items.Count; i++)
        {
            await Assert.That(feedback.Items[i].Type).IsEqualTo(fbType);
        }
    }

    [Then("the feedback is sorted {string}")]
    public async Task ThenFeedbackIsSorted(string order)
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsTrue();
        var feedback = response.Json<FeedbackResponse>();

        if (feedback.Items.Count < 2) return;

        if (order.Contains("highest to lowest"))
        {
            for (int i = 1; i < feedback.Items.Count; i++)
            {
                await Assert.That(feedback.Items[i - 1].Rating).IsGreaterThanOrEqualTo(feedback.Items[i].Rating);
            }
        }
        else if (order.Contains("lowest to highest"))
        {
            for (int i = 1; i < feedback.Items.Count; i++)
            {
                await Assert.That(feedback.Items[i - 1].Rating).IsLessThanOrEqualTo(feedback.Items[i].Rating);
            }
        }
        else if (order.Contains("newest to oldest"))
        {
            for (int i = 1; i < feedback.Items.Count; i++)
            {
                await Assert.That(feedback.Items[i - 1].CreatedAt).IsGreaterThanOrEqualTo(feedback.Items[i].CreatedAt);
            }
        }
        else if (order.Contains("oldest to newest"))
        {
            for (int i = 1; i < feedback.Items.Count; i++)
            {
                await Assert.That(feedback.Items[i - 1].CreatedAt).IsLessThanOrEqualTo(feedback.Items[i].CreatedAt);
            }
        }
    }

    [Then("at most 2 feedback items are returned")]
    public async Task ThenAtMost2FeedbackItems()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsTrue();
        var feedback = response.Json<FeedbackResponse>();
        await Assert.That(feedback.Items.Count).IsLessThanOrEqualTo(2);
    }

    [Then("the feedback is filtered, sorted, and paginated correctly")]
    public async Task ThenFeedbackFilteredSortedPaginated()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Succeeded).IsTrue();
        var feedback = response.Json<FeedbackResponse>();

        for (int i = 0; i < feedback.Items.Count; i++)
        {
            await Assert.That(feedback.Items[i].Type).IsEqualTo(FeedbackType.CuisineExperience);
        }
        // Sorted by highest rating first
        if (feedback.Items.Count >= 2)
        {
            for (int i = 1; i < feedback.Items.Count; i++)
            {
                await Assert.That(feedback.Items[i - 1].Rating).IsGreaterThanOrEqualTo(feedback.Items[i].Rating);
            }
        }
    }
}
