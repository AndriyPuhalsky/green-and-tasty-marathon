using RestaurantAqa.Screenplay.Core;

namespace RestaurantAqa.Screenplay.Abilities;

/// <summary>Обліковий запис у Cognito, створений для цього прогону.</summary>
public sealed class HaveAnAccount(string firstName, string lastName, string email, string password) : IAbility
{
    public string FirstName { get; } = firstName;
    public string LastName { get; } = lastName;
    public string Email { get; } = email;
    public string Password { get; } = password;

    public string FullName => $"{FirstName} {LastName}";

    public string? UserId { get; set; }
}
