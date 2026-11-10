namespace TickPulse.Core;

// A listed symbol in the synthetic market. All tickers are invented and
// none of them map to a real company.
public sealed record Symbol
{
    public required string Ticker { get; init; }
    public required string Name { get; init; }
    public required decimal BasePrice { get; init; }

    // Typical size of one random price move, as a fraction of price.
    // 0.002 means a normal move is about 0.2 percent.
    public required decimal Volatility { get; init; }
}

// One price update for one symbol.
public sealed record MarketTick
{
    public required string Ticker { get; init; }
    public required decimal Price { get; init; }
    public required long Volume { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    // Percent change from the previous tick for this symbol.
    public required decimal ChangePercent { get; init; }
}

// One entry on a user's watchlist.
public sealed record WatchlistItem
{
    public int Id { get; init; }
    public required string Ticker { get; init; }
    public required DateTimeOffset AddedAt { get; init; }
}

// The kinds of odd behaviour the analyst watches for.
public enum AnomalyType
{
    PriceJump,
    StaleTicks,
    MissingTicks
}

// A detected anomaly, before any explanation is attached.
public sealed record Anomaly
{
    public required string Ticker { get; init; }
    public required AnomalyType Type { get; init; }
    public required decimal ChangePercent { get; init; }
    public required DateTimeOffset DetectedAt { get; init; }

    // Short factual summary, for example "PULS moved 4.2 percent in one tick".
    public required string Summary { get; init; }
}
