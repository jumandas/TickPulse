using TickPulse.Analyst;
using TickPulse.Core;
using Xunit;

namespace TickPulse.Tests;

public sealed class RuleBasedAnalystTests
{
    [Fact]
    public async Task PriceJumpExplanationMentionsTickerAndDirection()
    {
        var analyst = new RuleBasedMarketAnalyst();
        var anomaly = new Anomaly
        {
            Ticker = "QNTM",
            Type = AnomalyType.PriceJump,
            ChangePercent = 4.2m,
            DetectedAt = DateTimeOffset.UtcNow,
            Summary = "QNTM rose 4.20 percent in one tick."
        };
        var tick = new MarketTick
        {
            Ticker = "QNTM",
            Price = 440.5m,
            Volume = 12000,
            Timestamp = DateTimeOffset.UtcNow,
            ChangePercent = 4.2m
        };

        var text = await analyst.ExplainAsync(anomaly, tick);

        Assert.Contains("QNTM", text);
        Assert.Contains("4.20", text);
        Assert.Contains("up", text);
    }

    [Fact]
    public async Task StaleExplanationMentionsTheQuietSymbol()
    {
        var analyst = new RuleBasedMarketAnalyst();
        var anomaly = new Anomaly
        {
            Ticker = "GLOB",
            Type = AnomalyType.StaleTicks,
            ChangePercent = 0,
            DetectedAt = DateTimeOffset.UtcNow,
            Summary = "GLOB has been quiet for 12 seconds."
        };

        var text = await analyst.ExplainAsync(anomaly, null);

        Assert.Contains("GLOB", text);
        Assert.Contains("quiet", text);
    }

    [Fact]
    public void AnalystNameIsRuleBased()
    {
        IMarketAnalyst analyst = new RuleBasedMarketAnalyst();
        Assert.Equal("rule-based", analyst.Name);
    }
}
