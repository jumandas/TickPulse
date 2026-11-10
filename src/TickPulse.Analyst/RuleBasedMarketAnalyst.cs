using TickPulse.Core;

namespace TickPulse.Analyst;

// Explains anomalies with fixed templates. This is the default analyst:
// it needs no API key, no network, and always works. The API project
// switches to the OpenAI analyst only when a key is configured.
public sealed class RuleBasedMarketAnalyst : IMarketAnalyst
{
    public string Name => "rule-based";

    public Task<string> ExplainAsync(Anomaly anomaly, MarketTick? tick, CancellationToken cancellationToken = default)
    {
        var text = anomaly.Type switch
        {
            AnomalyType.PriceJump => PriceJumpText(anomaly, tick),
            AnomalyType.StaleTicks => $"{anomaly.Ticker} has gone quiet. {anomaly.Summary} In a live feed this usually means the data provider dropped the connection or the symbol stopped trading for a moment. TickPulse keeps the last known price on screen until fresh ticks arrive.",
            AnomalyType.MissingTicks => $"{anomaly.Ticker} has not produced any ticks yet. {anomaly.Summary} The tick generator may still be warming up, or the symbol may be paused. Nothing is wrong with the feed itself.",
            _ => anomaly.Summary
        };

        return Task.FromResult(text);
    }

    private static string PriceJumpText(Anomaly anomaly, MarketTick? tick)
    {
        var direction = anomaly.ChangePercent > 0 ? "up" : "down";
        var size = Math.Abs(anomaly.ChangePercent);
        var sizeWord = size >= 5 ? "a large move" : "a sharp move";
        var volume = tick is null ? "unknown volume" : $"volume of {tick.Volume:N0} shares";
        var price = tick is null ? "an unknown price" : $"a price of {tick.Price:0.00}";

        return $"{anomaly.Ticker} moved {direction} {size:0.00} percent in a single tick, which is {sizeWord} for this feed. " +
               $"The tick arrived at {price} with {volume}. " +
               "In a real market a move like this can follow news, an earnings surprise, or a large order. " +
               "Here the data is synthetic, so the move came from the random walk generator, but the pattern is the same one a trader would want to see flagged.";
    }
}
