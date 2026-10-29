using RestaurantAqa.Screenplay.Core;

namespace RestaurantAqa.Screenplay.Abilities;

/// <summary>Гість без облікового запису: має лише гостьову сесію з токеном, який видав бекенд.</summary>
public sealed class JoinAsGuest(string guestName) : IAbility
{
    public string GuestName { get; } = guestName;

    public string? SessionId { get; private set; }

    public string? SessionToken { get; private set; }

    public void Joined(string sessionId, string sessionToken)
    {
        SessionId = sessionId;
        SessionToken = sessionToken;
    }

    public string RequireSessionId()
        => SessionId ?? throw new InvalidOperationException($"{GuestName} has not joined any group yet.");
}
