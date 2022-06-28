using Microsoft.AspNetCore.WebUtilities;

var builder = WebApplication.CreateBuilder(args);
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

app.Run();
