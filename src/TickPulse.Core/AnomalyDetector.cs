namespace TickPulse.Core;

// Watches the tick stream and flags behaviour worth explaining: sudden
// price jumps and symbols that go quiet. It is pure logic with no I/O,
// so it is easy to test.
public sealed class AnomalyDetector
{
    private readonly Dictionary<string, (decimal Price, DateTimeOffset SeenAt)> _lastSeen = new(StringComparer.OrdinalIgnoreCase);

    // A move bigger than this, in percent, counts as a jump.
    public decimal JumpThresholdPercent { get; init; } = 2.5m;

    // Silence longer than this counts as stale.
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(5);

    // Checks one fresh tick. Returns an anomaly when the move is a jump.
    public Anomaly? Check(MarketTick tick)
    {
        var hadPrevious = _lastSeen.TryGetValue(tick.Ticker, out var previous);
        _lastSeen[tick.Ticker] = (tick.Price, tick.Timestamp);

        if (!hadPrevious)
        {
            return null;
        }

        if (Math.Abs(tick.ChangePercent) < JumpThresholdPercent)
        {
            return null;
        }

        var direction = tick.ChangePercent > 0 ? "rose" : "fell";
        return new Anomaly
        {
            Ticker = tick.Ticker,
            Type = AnomalyType.PriceJump,
            ChangePercent = tick.ChangePercent,
            DetectedAt = tick.Timestamp,
            Summary = $"{tick.Ticker} {direction} {Math.Abs(tick.ChangePercent):0.00} percent in one tick, from {previous.Price:0.00} to {tick.Price:0.00}."
        };
    }

    // Checks one symbol for silence. Call this on a timer for every symbol
    // the engine is tracking. Returns a stale anomaly when the last tick
    // is older than StaleAfter, and a missing anomaly when the symbol has
    // never produced a tick at all.
    public Anomaly? CheckHeartbeat(string ticker, DateTimeOffset now)
    {
        if (!_lastSeen.TryGetValue(ticker, out var previous))
        {
            return new Anomaly
            {
                Ticker = ticker,
                Type = AnomalyType.MissingTicks,
                ChangePercent = 0,
                DetectedAt = now,
                Summary = $"{ticker} has not produced any ticks yet."
            };
        }

        var quietFor = now - previous.SeenAt;
        if (quietFor < StaleAfter)
        {
            return null;
        }

        return new Anomaly
        {
            Ticker = ticker,
            Type = AnomalyType.StaleTicks,
            ChangePercent = 0,
            DetectedAt = now,
            Summary = $"{ticker} has been quiet for {quietFor.TotalSeconds:0} seconds. Last price was {previous.Price:0.00}."
        };
    }
}
