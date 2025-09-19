// Sample adopted from https://opentelemetry.io/docs/languages/dotnet/getting-started

using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddControllers();
// Register HttpClient for dependency injection
builder.Services.AddHttpClient();

var app = builder.Build();

var logger = app.Logger;

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Define the endpoint that calls service-b
app.MapGet("/", async context =>
{
    var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
    using var client = httpClientFactory.CreateClient();

    string serviceBUrl = app.Configuration["service-b:BaseUrl"] ?? "https://service-b-374419059356.us-east1.run.app"; // Default for local testing
                                                                                                                                
    logger.LogDebug($"Application Log: service-a: Invoking service-b at {serviceBUrl}/RollDice ...", serviceBUrl);

    try
    {
        // 1. Get an Identity Token from the Cloud Run metadata server
        // This token is audience-specific to the service-b service URL.
        string idToken = await GetCloudRunIdTokenAsync(serviceBUrl);

        if (!string.IsNullOrEmpty(idToken))
        {
            // 2. Add the Authorization header to the request to App2
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
            logger.LogDebug("Application Log: service-a: Attached Authorization: Bearer token to request for service-b.");
        }
        else if (app.Environment.IsProduction()) // Only warn in production if token is missing
        {
            logger.LogDebug("Application Log: service-a: ERROR - Could not retrieve ID token. service-b call will fail as authentication is required.");
        }

        var response = await client.GetAsync($"{serviceBUrl}/RollDice");
        response.EnsureSuccessStatusCode();
        var serviceBResponseContent = await response.Content.ReadAsStringAsync();

        var finalMessage = $"service-a received from service-b: \"{serviceBResponseContent}\"\r\n";
        await context.Response.WriteAsync(finalMessage);
    }
    catch (HttpRequestException e)
    {
        logger.LogDebug($"Application Log: service-a: Error invoking service-b: {e.Message}", e.Message);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync($"Error communicating with service-b: {e.Message}");
    }
    catch (Exception e)
    {
        logger.LogDebug($"Application Log: service-a: An unexpected error occurred: {e.Message}", e.Message);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync($"An unexpected error occurred: {e.Message}");
    }
});


app.Run();

// Helper method to get the ID Token from the Cloud Run metadata server
async Task<string> GetCloudRunIdTokenAsync(string audience)
{
    // Cloud Run metadata server address
    const string metadataServerUrl = "http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/identity";

    // This check ensures we only try to hit the metadata server when running in a GCP environment
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("K_SERVICE")) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("K_REVISION")))
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Metadata-Flavor", "Google");

            // Request an ID token with the target App2 URL as the audience
            var response = await httpClient.GetAsync($"{metadataServerUrl}?audience={audience}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug($"Application Log: Error retrieving ID token from metadata server: {ex.Message}", ex.Message);
            return null;
        }
    }
    logger.LogDebug("Application Log: Not running in Cloud Run environment. Skipping ID token retrieval.");
    return null; // Not in Cloud Run, no token to get
}
