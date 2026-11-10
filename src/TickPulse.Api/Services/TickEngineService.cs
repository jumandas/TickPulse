using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TickPulse.Api.Data;
using TickPulse.Api.Hubs;
using TickPulse.Core;

namespace TickPulse.Api.Services;

// The heartbeat of the app. Every interval it generates one synthetic
// tick per symbol, then fans it out three ways: the in-memory latest
// quote store, Redis pub/sub, and SignalR. Ticks are also queued for
// the analyst worker and saved to Postgres in batches.
public sealed class TickEngineService : BackgroundService
{
    private readonly TickGenerator _generator;
    private readonly LatestQuotes _latestQuotes;
    private readonly ITickPublisher _publisher;
    private readonly IHubContext<MarketHub> _hub;
    private readonly Channel<MarketTick> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TickEngineService> _logger;
    private readonly int _intervalMs;
    private readonly IConfiguration _configuration;
    private readonly List<MarketTick> _pendingSaves = new();

    public TickEngineService(
        TickGenerator generator,
        LatestQuotes latestQuotes,
        ITickPublisher publisher,
        IHubContext<MarketHub> hub,
        Channel<MarketTick> channel,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<TickEngineService> logger)
    {
        _generator = generator;
        _latestQuotes = latestQuotes;
        _publisher = publisher;
        _hub = hub;
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalMs = configuration.GetValue<int>("TickEngine:IntervalMs", 250);
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var symbols = await LoadSymbolsAsync(stoppingToken);
        if (symbols.Count == 0)
        {
            _logger.LogWarning("No symbols configured, tick engine is idle.");
            return;
        }

        _logger.LogInformation("Tick engine started for {Count} symbols, one tick every {Interval} ms.", symbols.Count, _intervalMs);

        var lastSave = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var symbol in symbols)
            {
                if (_generator.ShouldSkipTick())
                {
                    continue;
                }

                var tick = _generator.NextTick(symbol, now);
                _latestQuotes.Update(tick);
                _pendingSaves.Add(tick);
                await _channel.Writer.WriteAsync(tick, stoppingToken);
                await _publisher.PublishAsync(tick, stoppingToken);
                await _hub.Clients.Group($"ticker:{tick.Ticker}").SendAsync("Tick", tick, stoppingToken);
                await _hub.Clients.Group("all").SendAsync("Tick", tick, stoppingToken);
            }

            if (now - lastSave >= TimeSpan.FromSeconds(2) && _pendingSaves.Count > 0)
            {
                await SavePendingTicksAsync();
                lastSave = now;
            }

            try
            {
                await Task.Delay(_intervalMs, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        if (_pendingSaves.Count > 0)
        {
            await SavePendingTicksAsync();
        }
    }

    // Symbols come from the database when it is up. When Postgres is
    // down, the engine falls back to the symbols in configuration so
    // the live feed keeps running and only history is affected.
    private async Task<List<Symbol>> LoadSymbolsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TickPulseDbContext>();
            var fromDb = await db.Symbols.OrderBy(s => s.Ticker).ToListAsync(cancellationToken);
            if (fromDb.Count > 0)
            {
                return fromDb;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load symbols from the database, falling back to configuration.");
        }

        return _configuration.GetSection("TickEngine:Symbols").Get<List<Symbol>>() ?? new List<Symbol>();
    }

    private async Task SavePendingTicksAsync()
    {
        var batch = _pendingSaves.ToList();
        _pendingSaves.Clear();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TickPulseDbContext>();
            db.Ticks.AddRange(batch);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save a batch of {Count} ticks. The live feed is not affected.", batch.Count);
        }
    }
}
