using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TickPulse.Api.Data;
using TickPulse.Api.Hubs;
using TickPulse.Api.Services;
using TickPulse.Analyst;
using TickPulse.Core;

var builder = WebApplication.CreateBuilder(args);

// Config: connection strings and tick settings live in appsettings.json.
// The OpenAI key is never in a config file. It is read from the
// OPENAI_API_KEY environment variable, and only then is the OpenAI
// analyst used. Without it the app runs on the rule-based analyst.
var openAiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// CORS is open for local development so the Angular dev server on
// port 4200 can call the API on port 5000 without friction.
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddDbContext<TickPulseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TickPulseDb")));

// Shared state between the engine, the controllers, and the analyst.
builder.Services.AddSingleton<LatestQuotes>();
builder.Services.AddSingleton(TickGeneratorFactory.CreateChannel());
builder.Services.AddSingleton<ITickPublisher, RedisTickPublisher>();
builder.Services.AddSingleton<AnomalyDetector>();
builder.Services.AddSingleton(new TickGenerator());
builder.Services.AddHostedService<TickEngineService>();
builder.Services.AddHostedService<AnalystWorker>();

// Analyst selection: OpenAI when a key is configured, rule-based when not.
if (!string.IsNullOrWhiteSpace(openAiKey))
{
    var analystOptions = new AnalystOptions
    {
        ApiKey = openAiKey,
        Model = builder.Configuration["OpenAI:Model"] ?? "gpt-4o-mini"
    };
    builder.Services.AddHttpClient<OpenAiMarketAnalyst>();
    builder.Services.AddSingleton<IMarketAnalyst>(sp =>
        new OpenAiMarketAnalyst(
            sp.GetRequiredService<HttpClient>(),
            analystOptions));
}
else
{
    builder.Services.AddSingleton<IMarketAnalyst, RuleBasedMarketAnalyst>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHub<MarketHub>("/hubs/market");

// Create the database and seed the synthetic symbols on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TickPulseDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await db.Database.EnsureCreatedAsync();
        await SymbolSeeder.SeedAsync(db, app.Configuration);
        logger.LogInformation("Database ready and symbols seeded.");
    }
    catch (Exception ex)
    {
        // The API still starts without a database. Live ticks and quotes
        // keep working from memory, only history and watchlists are down.
        logger.LogWarning(ex, "Database was not available at startup. History and watchlist endpoints will fail until Postgres is up.");
    }
}

app.Run();

// Needed so the test project can host the API in memory.
public partial class Program { }
