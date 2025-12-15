using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));
});
var app = builder.Build();
app.UseForwardedHeaders();

string storedPath = string.Empty;
string storedToken = string.Empty;
string storedValue = string.Empty;

app.MapPost("/.well-known/set/{path}/{token}", async (HttpContext context, string path, string token, ILogger<Program> logger) =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        var actualPath = context.Request.Path.Value ?? string.Empty;
        try
        {
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
            var payload = await JsonSerializer.DeserializeAsync<DataPayload>(context.Request.Body, options);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Data))
            {
                return Results.BadRequest();
            }

            storedPath = path;
            storedToken = token;
            storedValue = payload.Data;

            logger.LogInformation("{clientIp} requested: {actualPath}", clientIp, actualPath);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError("Path: {path} | Token: {token} | Requestor: {clientIp} | Error: {ex.Message}", path, token, clientIp, ex.Message);
            return Results.BadRequest();
        }
    }
);

app.MapGet("/{**requestedPath}", (HttpContext context, ILogger<Program> logger) =>
{
    var clientIp = context.Connection.RemoteIpAddress?.ToString();
    var actualPath = context.Request.Path.Value ?? string.Empty;

    const string wellKnownPrefix = "/.well-known/";

    if (!actualPath.StartsWith(wellKnownPrefix, StringComparison.OrdinalIgnoreCase))
    {
        logger.LogInformation("{clientIp} requested: {actualPath}", clientIp, actualPath);
        return Results.NotFound();
    }

    var normalizedPath = actualPath.Substring(wellKnownPrefix.Length);
    var expectedPath = $"{storedPath}/{storedToken}";

    if (!string.Equals(normalizedPath, expectedPath, StringComparison.Ordinal))
    {
        logger.LogInformation("{clientIp} requested: {actualPath}", clientIp, actualPath);
        return Results.NotFound();
    }

    logger.LogInformation("Validation value served for {clientIp}", clientIp);

    return Results.Text(storedValue, "text/plain");
});

app.MapGet("/clear", (HttpContext context, ILogger<Program> logger) =>
{
    var clientIp = context.Connection.RemoteIpAddress?.ToString();
    storedValue = string.Empty;
    storedPath = string.Empty;
    storedToken = string.Empty;

    logger.LogInformation("Validation value cleared. Requestor {clientIp}", clientIp);

    return Results.NoContent();
});

app.Run();

record DataPayload(string Data);
