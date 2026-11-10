using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TickPulse.Analyst;
using TickPulse.Api.Data;
using TickPulse.Api.Hubs;
using TickPulse.Core;

namespace TickPulse.Api.Services;

// Consumes ticks from the engine, runs anomaly detection, and asks the
// analyst to explain anything odd. Explanations are stored in Postgres
// and pushed live to SignalR clients in the "analyst" group. The analyst
// never blocks the tick feed: if it fails, the worker logs and moves on.
public sealed class AnalystWorker : BackgroundService
{
    private readonly Channel<MarketTick> _channel;
    private readonly AnomalyDetector _detector;
    private readonly IMarketAnalyst _analyst;
    private readonly IHubContext<MarketHub> _hub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalystWorker> _logger;

    // Last time a stale or missing event was emitted per ticker, so a
    // quiet symbol does not spam one event per heartbeat.
    private readonly Dictionary<string, DateTimeOffset> _lastHeartbeatEvent = new(StringComparer.OrdinalIgnoreCase);

    public AnalystWorker(
        Channel<MarketTick> channel,
        AnomalyDetector detector,
        IMarketAnalyst analyst,
        IHubContext<MarketHub> hub,
        IServiceScopeFactory scopeFactory,
        ILogger<AnalystWorker> logger)
    {
        _channel = channel;
        _detector = detector;
        _analyst = analyst;
        _hub = hub;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Analyst worker started with the {Name} analyst.", _analyst.Name);

        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        var reading = ReadTicksAsync(stoppingToken);
        var heartbeats = RunHeartbeatsAsync(heartbeatTimer, stoppingToken);
        await Task.WhenAll(reading, heartbeats);
    }

    private async Task ReadTicksAsync(CancellationToken stoppingToken)
    {
        await foreach (var tick in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            var anomaly = _detector.Check(tick);
            if (anomaly is not null)
            {
                await HandleAnomalyAsync(anomaly, tick, stoppingToken);
            }
        }
    }

    private async Task RunHeartbeatsAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            List<string> tickers;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TickPulseDbContext>();
                tickers = await db.Symbols.Select(s => s.Ticker).ToListAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Heartbeat check skipped, symbols could not be loaded.");
                continue;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var ticker in tickers)
            {
                var anomaly = _detector.CheckHeartbeat(ticker, now);
                if (anomaly is null)
                {
                    continue;
                }

                if (_lastHeartbeatEvent.TryGetValue(ticker, out var last) && now - last < TimeSpan.FromSeconds(30))
                {
                    continue;
                }

                _lastHeartbeatEvent[ticker] = now;
                await HandleAnomalyAsync(anomaly, null, stoppingToken);
            }
        }
    }

    private async Task HandleAnomalyAsync(Anomaly anomaly, MarketTick? tick, CancellationToken stoppingToken)
    {
        string explanation;
        try
        {
            explanation = await _analyst.ExplainAsync(anomaly, tick, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Analyst {Name} failed for {Ticker}, falling back to the rule-based analyst.", _analyst.Name, anomaly.Ticker);
            explanation = await new RuleBasedMarketAnalyst().ExplainAsync(anomaly, tick, stoppingToken);
        }

        var record = new AnomalyEventRecord
        {
            Ticker = anomaly.Ticker,
            Type = anomaly.Type,
            ChangePercent = anomaly.ChangePercent,
            DetectedAt = anomaly.DetectedAt,
            Summary = anomaly.Summary,
            Explanation = explanation,
            AnalystName = _analyst.Name
        };

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TickPulseDbContext>();
            db.AnomalyEvents.Add(record);
            await db.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save analyst event for {Ticker}.", anomaly.Ticker);
        }

        await _hub.Clients.Group("analyst").SendAsync("AnalystEvent", record, stoppingToken);
        _logger.LogInformation("Analyst event for {Ticker}: {Summary}", anomaly.Ticker, anomaly.Summary);
    }
}
