using System.Text.Json.Serialization;

namespace RestaurantAqa.Api;

// Відповіді API, які потрібні тестам. Бекенд частину відповідей віддає в PascalCase,
// тому десеріалізація нечутлива до регістру (JsonSerializerDefaults.Web).

public sealed record SignInResponse(string IdToken, string AccessToken);

public sealed record LocationResponse(string Id, string Address);

public sealed record TableResponse(string Id, string LocationId, int Capacity, int TableNumber);

public sealed record DishResponse(string Id, string Name, decimal Price);

public sealed record ReservationResponse(
    string Id,
    string TableId,
    DateTime StartTime,
    DateTime EndTime,
    int GuestCount,
    int Status,
    string? CustomerId,
    string? WaiterId);

public sealed record InviteTokenResponse(string Token);

public sealed record GroupInviteResponse(
    string GroupReservationId,
    string LocationAddress,
    DateTime StartTime,
    int MaxGuestCount,
    int TableNumber);

public sealed record GuestSessionCreated(string SessionId, string SessionToken);

public sealed record OwnerSessionResponse(string GuestSessionId);

public sealed record GroupSnapshot(IReadOnlyList<ParticipantResponse> Participants, decimal Total);

public sealed record ParticipantResponse(
    string Id,
    string Type,
    string Name,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Total,
    bool IsSubmitted);

public sealed record OrderItemResponse(DishResponse Dish, int Quantity);

public sealed record GroupStatusResponse(int CurrentParticipants, int MaxCapacity, string Status);

public sealed record SubmittedDish(string DishId, int Quantity);

public sealed record OrderResponse(
    string OrderId,
    string ReservationId,
    IReadOnlyList<OrderItemResponse> OrderItems,
    int Status);

/// <summary>Статуси замовлення так, як їх зберігає бекенд (enum OrderStatus).</summary>
public enum OrderStatus
{
    Created = 0,
    Submitted = 1,
    Cancelled = 2,
    SubmittedByCustomer = 3,
}

public sealed record CreateReservationRequest(
    [property: JsonPropertyName("tableId")] string TableId,
    [property: JsonPropertyName("startTime")] DateTime StartTime,
    [property: JsonPropertyName("endTime")] DateTime EndTime,
    [property: JsonPropertyName("guestCount")] int GuestCount);
