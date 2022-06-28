using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGet("authorize", (IConfiguration configuration) =>
{
    const string authorizeEndpoint = "https://notify-bot.line.me/oauth/authorize";

    var parameters = new Dictionary<string, string>()
    {
        ["response_type"] = "code",
        ["client_id"] = configuration["Authentication:LineNotify:ClientId"],
        ["redirect_uri"] = "https://localhost:44320/signin-line-notify",
        ["scope"] = "notify",
        ["state"] = "abcd"
    };

    var requestUri = QueryHelpers.AddQueryString(authorizeEndpoint, parameters);

    return Results.Redirect(requestUri);
});

app.MapGet("signin-line-notify",
async (string code, string state, IHttpClientFactory httpClientFactory, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(code))
    {
        return Results.BadRequest();
    }

    var keyValuePairs = new List<KeyValuePair<string, string>>
    {
        new("grant_type", "authorization_code"),
        new("code", code),
        new("redirect_uri", "https://localhost:44320/signin-line-notify"),
        new("client_id", configuration["Authentication:LineNotify:ClientId"]),
        new("client_secret", configuration["Authentication:LineNotify:ClientSecret"])
    };

    using var client = httpClientFactory.CreateClient();

    const string tokenEndpoint = "https://notify-bot.line.me/oauth/token";
    var encodedContent = new FormUrlEncodedContent(keyValuePairs);

    using var httpResponseMessage = await client.PostAsync(tokenEndpoint, encodedContent);

    if (!httpResponseMessage.IsSuccessStatusCode)
    {
        string message = await httpResponseMessage.Content.ReadAsStringAsync();
        throw new Exception(message);
    }

    var jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    var tokenResponse = await httpResponseMessage.Content.ReadFromJsonAsync<TokenResponse>(jsonSerializerOptions);

    if (tokenResponse == null)
    {
        throw new Exception("token content failed");
    }

    return Results.Json(new { tokenResponse.AccessToken });
});

app.MapPost("notify", async (Notify input, IHttpClientFactory httpClientFactory, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(input.AccessToken) || string.IsNullOrWhiteSpace(input.Message))
    {
        return Results.BadRequest();
    }

    using var client = httpClientFactory.CreateClient();

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", input.AccessToken);

    var keyValuePairs = new List<KeyValuePair<string?, string?>>
    {
        new("message", input.Message)
    };

    const string notifyEndpoint = "https://notify-api.line.me/api/notify";
    var encodedContent = new FormUrlEncodedContent(keyValuePairs);

    using var httpResponseMessage = await client.PostAsync(notifyEndpoint, encodedContent);

    if (!httpResponseMessage.IsSuccessStatusCode)
    {
        string responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
        throw new Exception(responseContent);
    }

    return Results.Ok();
});

app.Run();

public class TokenResponse
{
    public int Status { get; set; }

    public string Message { get; set; }

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
}

public class Notify
{
    public string Message { get; set; }

    public string AccessToken { get; set; }
}