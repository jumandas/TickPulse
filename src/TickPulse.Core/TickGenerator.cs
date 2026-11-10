namespace TickPulse.Core;

// Generates synthetic market ticks with a random walk. Prices can drift,
// jump suddenly, and occasionally skip a beat, which is what makes the
// feed interesting to watch. Nothing here touches real market data.
public sealed class TickGenerator
{
    private readonly Random _random;
    private readonly Dictionary<string, decimal> _lastPrice = new(StringComparer.OrdinalIgnoreCase);

    // Chance that a tick is a sharp jump instead of a normal move.
    public double JumpProbability { get; init; } = 0.02;

    // Chance that a symbol simply produces no tick this round (a gap).
    public double GapProbability { get; init; } = 0.01;

    // Smallest and largest jump size, as a fraction of price.
    public decimal MinJump { get; init; } = 0.03m;
    public decimal MaxJump { get; init; } = 0.08m;

    public TickGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    // True when this symbol should skip its tick this round, simulating
    // a dropped message in the feed.
    public bool ShouldSkipTick()
    {
        return _random.NextDouble() < GapProbability;
    }

    public MarketTick NextTick(Symbol symbol, DateTimeOffset now)
    {
        var previous = _lastPrice.TryGetValue(symbol.Ticker, out var last)
            ? last
            : symbol.BasePrice;

        decimal price;
        if (_random.NextDouble() < JumpProbability)
        {
            var direction = _random.NextDouble() < 0.5 ? -1m : 1m;
            var size = MinJump + (decimal)_random.NextDouble() * (MaxJump - MinJump);
            price = previous * (1 + direction * size);
        }
        else
        {
            var move = (decimal)NextNormal() * symbol.Volatility;
            price = previous * (1 + move);
        }

        if (price <= 0)
        {
            price = previous * 0.5m;
        }

        price = Math.Round(price, 2);
        _lastPrice[symbol.Ticker] = price;

        var changePercent = previous == 0
            ? 0
            : Math.Round((price - previous) / previous * 100, 2);

        return new MarketTick
        {
            Ticker = symbol.Ticker,
            Price = price,
            Volume = _random.Next(100, 100_000),
            Timestamp = now,
            ChangePercent = changePercent
        };
    }

    // Box-Muller transform, gives a roughly normal value around zero.
    private double NextNormal()
    {
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
