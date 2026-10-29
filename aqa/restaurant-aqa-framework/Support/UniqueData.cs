using System.Security.Cryptography;

namespace RestaurantAqa.Support;

/// <summary>Унікальні дані на кожен запуск: користувачів Cognito через API не видалити, тому e-mail завжди нові.</summary>
public static class UniqueData
{
    private static int _tableCounter = RandomNumberGenerator.GetInt32(100, 900) * 100;

    public static string RunId { get; } = $"{DateTime.UtcNow:MMddHHmm}{RandomLetters(3)}";

    public static string Email(string role)
    {
        var settings = TestSettings.Current;
        return $"{settings.EmailPrefix}.{RunId}.{role}.{Guid.NewGuid().ToString("N")[..8]}@{settings.EmailDomain}".ToLowerInvariant();
    }

    // Cognito: великі й малі літери, цифра і символ; бекенд: 8-16 символів
    public static string Password() => $"Aqa{RandomLetters(5)}7!";

    // ім'я та прізвище: лише латинські літери, дефіс і апостроф
    public static string LastName() => char.ToUpperInvariant(RandomLetters(1)[0]) + RandomLetters(6);

    public static int NextTableNumber() => Interlocked.Increment(ref _tableCounter);

    public static string RandomLetters(int length)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz";
        return string.Create(length, alphabet, (span, letters) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = letters[RandomNumberGenerator.GetInt32(letters.Length)];
            }
        });
    }
}
