using TickPulse.Core;
using Xunit;

namespace TickPulse.Tests;

public sealed class TickGeneratorTests
{
    private static Symbol TestSymbol() => new()
    {
        Ticker = "PULS",
        Name = "Pulse Health Systems",
        BasePrice = 100m,
        Volatility = 0.002m
    };

    [Fact]
    public void PricesStayPositiveOverManyTicks()
    {
        var generator = new TickGenerator(seed: 42);
        var symbol = TestSymbol();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 10000; i++)
        {
            var tick = generator.NextTick(symbol, now.AddMilliseconds(i * 250));
            Assert.True(tick.Price > 0);
            Assert.Equal("PULS", tick.Ticker);
        }
    }

    [Fact]
    public void SeededGeneratorIsDeterministic()
    {
        var first = new TickGenerator(seed: 7);
        var second = new TickGenerator(seed: 7);
        var symbol = TestSymbol();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 100; i++)
        {
            var a = first.NextTick(symbol, now.AddMilliseconds(i * 250));
            var b = second.NextTick(symbol, now.AddMilliseconds(i * 250));
            Assert.Equal(a.Price, b.Price);
            Assert.Equal(a.Volume, b.Volume);
        }
    }

    [Fact]
    public void JumpsHappenOverALongRun()
    {
        var generator = new TickGenerator(seed: 123) { JumpProbability = 0.02 };
        var symbol = TestSymbol();
        var now = DateTimeOffset.UtcNow;
        var jumps = 0;

        for (var i = 0; i < 5000; i++)
        {
            var tick = generator.NextTick(symbol, now.AddMilliseconds(i * 250));
            if (Math.Abs(tick.ChangePercent) >= 3m)
            {
                jumps++;
            }
        }

        Assert.True(jumps > 20, $"Expected many jumps in 5000 ticks, saw {jumps}.");
    }

    [Fact]
    public void ChangePercentMatchesPriceMove()
    {
        var generator = new TickGenerator(seed: 99);
        var symbol = TestSymbol();
        var now = DateTimeOffset.UtcNow;

        var first = generator.NextTick(symbol, now);
        var second = generator.NextTick(symbol, now.AddMilliseconds(250));

        var expected = Math.Round((second.Price - first.Price) / first.Price * 100, 2);
        Assert.Equal(expected, second.ChangePercent);
    }
}
