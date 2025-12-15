using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
var app = builder.Build();

string storedPath = string.Empty;
string storedToken = string.Empty;
string storedValue = string.Empty;

app.MapPost("/.well-known/set/{path}/{token}", async (HttpContext context, string path, string token, ILogger<Program> logger) =>
    {
        try
        {
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
            var payload = await JsonSerializer.DeserializeAsync<DataPayload>(context.Request.Body, options);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Data))
            {
                return Results.BadRequest("Invalid JSON body");
            }

            storedPath = path;
            storedToken = token;
            storedValue = payload.Data;

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError("Path: {path} | Token: {token} | Error: {ex.Message}", path, token, ex.Message);
            return Results.BadRequest();
        }
    }
);

app.MapGet("/{**requestedPath}", (HttpContext context, ILogger<Program> logger) =>
{
    var actualPath = context.Request.Path.Value ?? string.Empty;
    logger.LogInformation("{actualPath}", actualPath);

    const string wellKnownPrefix = "/.well-known/";

    if (!actualPath.StartsWith(wellKnownPrefix, StringComparison.OrdinalIgnoreCase))
    {
        return Results.NotFound();
    }

    var normalizedPath = actualPath.Substring(wellKnownPrefix.Length);
    var expectedPath = $"{storedPath}/{storedToken}";

    if (!string.Equals(normalizedPath, expectedPath, StringComparison.Ordinal))
    {
        return Results.NotFound();
    }
    var responseValue = storedValue;

    storedValue = string.Empty;
    storedPath = string.Empty;
    storedToken = string.Empty;

    logger.LogInformation("Validation value served and cleared");

    return Results.Text(responseValue, "text/plain");
});

app.Run();

record DataPayload(string Data);
