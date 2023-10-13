using AzureBok.Presentation.Settings;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Options;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Install Microsoft.Extensions.Configuration.AzureAppConfiguration package
// Install Microsoft.Azure.CognitiveServices.Vision.ComputerVision package
builder.Configuration
    .AddAzureAppConfiguration(options =>
    {
        options.Connect(builder.Configuration.GetConnectionString("AzureAppConfiguration"));
        // Select by Key and Label
        options
            .Select(KeyFilter.Any, LabelFilter.Null)
            .Select(KeyFilter.Any, "Test");
    });

// Create settings classes for Configuration
builder.Services.Configure<AzureSettings>(builder.Configuration);
builder.Services.Configure<AzureComputerVisionSettings>(builder.Configuration.GetSection("Azure:ComputerVision"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("name", (IOptions<AzureSettings> settings) => settings.Value);

app.MapGet("get-text/{imageUrl}", async (IOptions<AzureComputerVisionSettings> settings, string imageUrl) =>
{
    string key = settings.Value.Key;
    string endpoint = settings.Value.Endpoint;

    var client = Authenticate(endpoint, key);

    string decodedUrl = WebUtility.UrlDecode(imageUrl);

    return await ReadFileUrl(client, decodedUrl);
});
static ComputerVisionClient Authenticate(string endpoint, string key)
{
    var client =
      new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
      { Endpoint = endpoint };
    return client;
}

static async Task<IList<ReadResult>> ReadFileUrl(ComputerVisionClient client, string urlFile)
{
    Console.WriteLine("----------------------------------------------------------");
    Console.WriteLine("READ FILE FROM URL");
    Console.WriteLine();

    // Read text from URL
    var textHeaders = await client.ReadAsync(urlFile);
    // After the request, get the operation location (operation ID)
    string operationLocation = textHeaders.OperationLocation;

    // Retrieve the URI where the extracted text will be stored from the Operation-Location header.
    // We only need the ID and not the full URL
    const int numberOfCharsInOperationId = 36;
    string operationId = operationLocation[^numberOfCharsInOperationId..];

    // Extract the text
    ReadOperationResult results;
    Console.WriteLine($"Extracting text from URL file {Path.GetFileName(urlFile)}...");
    Console.WriteLine();
    do
    {
        results = await client.GetReadResultAsync(Guid.Parse(operationId));
    }
    while ((results.Status == OperationStatusCodes.Running ||
        results.Status == OperationStatusCodes.NotStarted));

    // Display the found text.
    Console.WriteLine();
    var textUrlFileResults = results.AnalyzeResult.ReadResults;
    return textUrlFileResults;
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
