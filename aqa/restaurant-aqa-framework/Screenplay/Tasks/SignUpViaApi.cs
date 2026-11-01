using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Registers a visitor via the API and remembers the credentials so later steps can sign in.
/// The email and password are remembered separately for retrieval by step definitions.</summary>
public sealed class SignUpViaApi : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var visitor = SampleDataFactory.RegisterRequest();
        actor.Remember(visitor);
        await actor.TheLastResponse().CaptureAsync(await new AuthApi(actor.UsingTheApi()).RegisterAsync(visitor));

        var outcome = await actor.AsksForAsync(new TheOutcome());
        if (!outcome.Succeeded)
            throw new InvalidOperationException($"Registration failed with {outcome.Status}: {outcome.Body}");

        // Remember email and password separately for step definitions
        actor.Remember("signupEmail", visitor.Email);
        actor.Remember("signupPassword", visitor.Password);
    }
}
