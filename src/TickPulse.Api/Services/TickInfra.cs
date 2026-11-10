using System.Collections.Concurrent;
using System.Threading.Channels;
using TickPulse.Core;

namespace TickPulse.Api.Services;

// Latest quote per ticker, kept in memory so the quote endpoints are
// fast even while the tick engine is running hot.
public sealed class LatestQuotes
{
    private readonly ConcurrentDictionary<string, MarketTick> _latest = new(StringComparer.OrdinalIgnoreCase);

    public void Update(MarketTick tick) => _latest[tick.Ticker] = tick;

    public MarketTick? Get(string ticker) =>
        _latest.TryGetValue(ticker, out var tick) ? tick : null;

    public IReadOnlyCollection<MarketTick> GetAll() =>
        _latest.Values.OrderBy(t => t.Ticker).ToList();
}

// Small factory so Program.cs stays readable. The channel carries ticks
// from the engine to the analyst worker without either knowing about
// the other.
public static class TickGeneratorFactory
{
    public static Channel<MarketTick> CreateChannel() =>
        Channel.CreateUnbounded<MarketTick>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
}

// Publishes ticks to Redis so other services could fan out from the same
// feed. Redis is optional: when it is down the publisher logs once and
// carries on, and the SignalR feed keeps working.
public interface ITickPublisher
{
    Task PublishAsync(MarketTick tick, CancellationToken cancellationToken = default);
}

public sealed class RedisTickPublisher : ITickPublisher, IDisposable
{
    private readonly ILogger<RedisTickPublisher> _logger;
    private readonly Lazy<StackExchange.Redis.IConnectionMultiplexer?> _connection;
    private bool _warned;

    public RedisTickPublisher(IConfiguration configuration, ILogger<RedisTickPublisher> logger)
    {
        _logger = logger;
        var connectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        _connection = new Lazy<StackExchange.Redis.IConnectionMultiplexer?>(() =>
        {
            try
            {
                return StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis is not reachable at {ConnectionString}. Tick publishing to Redis is off, SignalR still works.", connectionString);
                return null;
            }
        });
    }

    public async Task PublishAsync(MarketTick tick, CancellationToken cancellationToken = default)
    {
        var connection = _connection.Value;
        if (connection is null || !connection.IsConnected)
        {
            if (!_warned)
            {
                _logger.LogWarning("Redis is not connected, skipping tick publish for {Ticker}.", tick.Ticker);
                _warned = true;
            }

            return;
        }

        try
        {
            var db = connection.GetDatabase();
            var payload = System.Text.Json.JsonSerializer.Serialize(tick);
            await db.PublishAsync(new StackExchange.Redis.RedisChannel($"ticks:{tick.Ticker}", StackExchange.Redis.RedisChannel.PatternMode.Literal), payload);
            await db.StringSetAsync($"quote:{tick.Ticker}", payload, TimeSpan.FromMinutes(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish tick for {Ticker} to Redis.", tick.Ticker);
        }
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value?.Dispose();
        }
    }
}
