using RestaurantAqa.Api;
using RestaurantAqa.Screenplay.Abilities;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Support;

namespace RestaurantAqa.Screenplay.Tasks;

/// <summary>Реєстрація через POST /register. Бекенд одразу підтверджує користувача, листів Cognito не надсилає.</summary>
public sealed class RegisterAccount(string firstName, string lastName, string role) : ITask
{
    public static RegisterAccount WithName(string firstName, string lastName, string role = "customer")
        => new(firstName, lastName, role);

    public async Task PerformAs(Actor actor)
    {
        var account = new HaveAnAccount(firstName, lastName, UniqueData.Email(role), UniqueData.Password());
        var api = actor.AbilityTo<CallTheApi>();
        (await api.Post("/register", new
        {
            firstName = account.FirstName,
            lastName = account.LastName,
            email = account.Email,
            password = account.Password,
            confirmPassword = account.Password,
        })).EnsureSuccess();
        actor.WhoCan(account);
    }
}

public sealed class SignIn : ITask
{
    public static SignIn ThroughTheApi() => new();

    public async Task PerformAs(Actor actor)
    {
        var account = actor.AbilityTo<HaveAnAccount>();
        var api = actor.AbilityTo<CallTheApi>();
        var tokens = (await api.Post("/signin", new { email = account.Email, password = account.Password }))
            .EnsureSuccess().As<SignInResponse>();
        api.UseToken(tokens.IdToken);
        account.UserId = JwtClaims.Subject(tokens.IdToken);
    }
}

/// <summary>Офіціант = зареєстрований користувач, доданий у список офіціантів локації (POST /waiters).</summary>
public sealed class AssignAsWaiter(string locationId) : ITask
{
    public static AssignAsWaiter To(string locationId) => new(locationId);

    public async Task PerformAs(Actor actor)
    {
        var account = actor.AbilityTo<HaveAnAccount>();
        (await actor.AbilityTo<CallTheApi>().Post("/waiters", new { email = account.Email, locationId }))
            .EnsureSuccess();
    }
}
