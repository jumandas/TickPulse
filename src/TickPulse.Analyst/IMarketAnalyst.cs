using TickPulse.Core;

namespace TickPulse.Analyst;

// Settings for the market analyst. The API key is never read from a file
// that ships with the repo. It comes from the OPENAI_API_KEY environment
// variable, wired up in the API project.
public sealed class AnalystOptions
{
    public string? ApiKey { get; init; }
    public string Model { get; init; } = "gpt-4o-mini";
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
}

// Turns a detected anomaly into a plain-English explanation.
public interface IMarketAnalyst
{
    // Human readable name of the analyst in use, for example "rule-based"
    // or "openai". Shown in the UI so it is clear where text came from.
    string Name { get; }

    Task<string> ExplainAsync(Anomaly anomaly, MarketTick? tick, CancellationToken cancellationToken = default);
}
