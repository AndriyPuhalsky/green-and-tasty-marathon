using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RestaurantAqa.Api;
using RestaurantAqa.Screenplay.Core;
using RestaurantAqa.Support;

namespace RestaurantAqa.Screenplay.Abilities;

/// <summary>
/// Виклики REST API від імені актора. Токен у заголовку Authorization: idToken з Cognito
/// для клієнта чи офіціанта або токен гостьової сесії для гостя. Без токена актор анонімний.
/// </summary>
public sealed class CallTheApi : IAbility
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static int _retries;

    /// <summary>Скільки разів за прогін довелося повторити запит через перевантаження стенду.</summary>
    public static int Retries => _retries;

    private readonly string _baseUrl;
    private readonly ApiJournal? _journal;
    private string _actorName = "anonymous";

    private CallTheApi(string baseUrl, ApiJournal? journal)
    {
        _baseUrl = baseUrl;
        _journal = journal;
    }

    public static CallTheApi At(string baseUrl, ApiJournal? journal = null) => new(baseUrl, journal);

    public static CallTheApi OfTheStand(ApiJournal? journal = null) => At(TestSettings.Current.ApiUrl, journal);

    public string? BearerToken { get; private set; }

    public ApiResponse? LastResponse { get; private set; }

    public CallTheApi As(string actorName)
    {
        _actorName = actorName;
        return this;
    }

    public void UseToken(string? token) => BearerToken = token;

    public Task<ApiResponse> Get(string path) => Send(HttpMethod.Get, path);

    public Task<ApiResponse> Post(string path, object? body = null) => Send(HttpMethod.Post, path, body);

    public Task<ApiResponse> Put(string path, object? body = null) => Send(HttpMethod.Put, path, body);

    public Task<ApiResponse> Delete(string path, object? body = null) => Send(HttpMethod.Delete, path, body);

    public async Task<ApiResponse> Send(HttpMethod method, string path, object? body = null)
    {
        string? json = body is null ? null : JsonSerializer.Serialize(body, ApiResponse.Json);
        var stopwatch = Stopwatch.StartNew();
        ApiResponse response = await SendWithRetry(method, path, json);
        _journal?.Record(_actorName, method, path, json, response, stopwatch.Elapsed);
        LastResponse = response;
        return response;
    }

    // Lambda на стенді має невеликий ліміт одночасних викликів. Коли його перевищено, API Gateway
    // сам відповідає 500 {"message": "Internal server error"} із заголовком x-amzn-ErrorType
    // (або 429/503). До коду бекенду такі запити не доходили, тому їх безпечно повторити.
    private async Task<ApiResponse> SendWithRetry(HttpMethod method, string path, string? json)
    {
        const int attempts = 5;
        for (int attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, _baseUrl + path);
            if (json is not null)
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            if (!string.IsNullOrEmpty(BearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
            }

            using HttpResponseMessage message = await Http.SendAsync(request);
            string text = await message.Content.ReadAsStringAsync();
            var response = new ApiResponse(method, path, message.StatusCode, text);

            bool throttled = message.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable
                             || (message.StatusCode == HttpStatusCode.InternalServerError
                                 && message.Headers.Contains("x-amzn-ErrorType")
                                 && text.Contains("\"Internal server error\""))
                             // 504: бекенд не встиг за 29 с; повторюємо лише читання, бо запис міг уже виконатися
                             || (message.StatusCode == HttpStatusCode.GatewayTimeout && method == HttpMethod.Get);
            if (!throttled || attempt == attempts)
            {
                return response;
            }
            Interlocked.Increment(ref _retries);
            await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt * attempt + Random.Shared.Next(300)));
        }
    }
}
