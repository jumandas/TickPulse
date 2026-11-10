using TickPulse.Core;
using Xunit;

namespace TickPulse.Tests;

public sealed class AnomalyDetectorTests
{
    private static MarketTick Tick(string ticker, decimal price, decimal change, DateTimeOffset at) => new()
    {
        Ticker = ticker,
        Price = price,
        Volume = 1000,
        Timestamp = at,
        ChangePercent = change
    };

    [Fact]
    public void SmallMovesAreNotAnomalies()
    {
        var detector = new AnomalyDetector();
        var now = DateTimeOffset.UtcNow;

        Assert.Null(detector.Check(Tick("ACME", 100m, 0m, now)));
        Assert.Null(detector.Check(Tick("ACME", 100.5m, 0.5m, now.AddMilliseconds(250))));
        Assert.Null(detector.Check(Tick("ACME", 99.8m, -0.7m, now.AddMilliseconds(500))));
    }

    [Fact]
    public void LargeJumpIsDetected()
    {
        var detector = new AnomalyDetector();
        var now = DateTimeOffset.UtcNow;

        detector.Check(Tick("NOVA", 300m, 0m, now));
        var anomaly = detector.Check(Tick("NOVA", 315m, 5m, now.AddMilliseconds(250)));

        Assert.NotNull(anomaly);
        Assert.Equal(AnomalyType.PriceJump, anomaly!.Type);
        Assert.Equal("NOVA", anomaly.Ticker);
        Assert.Equal(5m, anomaly.ChangePercent);
        Assert.Contains("NOVA", anomaly.Summary);
    }

    [Fact]
    public void NegativeJumpIsDetectedToo()
    {
        var detector = new AnomalyDetector();
        var now = DateTimeOffset.UtcNow;

        detector.Check(Tick("VTX", 80m, 0m, now));
        var anomaly = detector.Check(Tick("VTX", 76m, -5m, now.AddMilliseconds(250)));

        Assert.NotNull(anomaly);
        Assert.Equal(AnomalyType.PriceJump, anomaly!.Type);
    }

    [Fact]
    public void QuietSymbolIsFlaggedStale()
    {
        var detector = new AnomalyDetector { StaleAfter = TimeSpan.FromSeconds(5) };
        var now = DateTimeOffset.UtcNow;

        detector.Check(Tick("ORBT", 56m, 0m, now));
        Assert.Null(detector.CheckHeartbeat("ORBT", now.AddSeconds(2)));

        var anomaly = detector.CheckHeartbeat("ORBT", now.AddSeconds(10));
        Assert.NotNull(anomaly);
        Assert.Equal(AnomalyType.StaleTicks, anomaly!.Type);
    }

    [Fact]
    public void UnknownSymbolIsFlaggedMissing()
    {
        var detector = new AnomalyDetector();
        var anomaly = detector.CheckHeartbeat("ZENX", DateTimeOffset.UtcNow);

        Assert.NotNull(anomaly);
        Assert.Equal(AnomalyType.MissingTicks, anomaly!.Type);
    }
}
