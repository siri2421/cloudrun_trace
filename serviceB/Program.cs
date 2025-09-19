var builder = WebApplication.CreateBuilder(args);

// The port to listen on is determined by the "PORT" environment variable,
// which is set by Cloud Run. Default to 8080 if not set.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var url = $"http://0.0.0.0:{port}";

var app = builder.Build();

var logger = app.Logger;

// API endpoint that takes a string and returns it with a random number appended.
int RollDice()
{
    logger.LogInformation("Application Log: In service-b.RollDice Method, sleeping for 1 seconds before rolling the dice...");
    Thread.Sleep(1000); // Simulate some work
    var randomNumber = Random.Shared.Next(100, 1000);
    logger.LogInformation("Application Log: Returning '{randomNumber}'", randomNumber);
    return randomNumber;
}


app.MapGet("/RollDice", RollDice);

app.Run(url);