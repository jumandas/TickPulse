using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TickPulse.Core;

namespace TickPulse.Analyst;

// Explains anomalies with the OpenAI chat API. The key is passed in
// through AnalystOptions, which the API project fills from the
// OPENAI_API_KEY environment variable. If a call fails for any reason,
// the caller falls back to the rule-based analyst so the app keeps
// running and the feed is never blocked by the AI call.
public sealed class OpenAiMarketAnalyst : IMarketAnalyst
{
    private readonly HttpClient _http;
    private readonly AnalystOptions _options;

    public OpenAiMarketAnalyst(HttpClient http, AnalystOptions options)
    {
        _http = http;
        _options = options;
    }

    public string Name => "openai";

    public async Task<string> ExplainAsync(Anomaly anomaly, MarketTick? tick, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        var prompt = BuildPrompt(anomaly, tick);
        var payload = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = "You are a market analyst explaining unusual price behaviour in plain English. Keep it to two or three short sentences. Note that the data is synthetic and simulated, not real market data." },
                new { role = "user", content = prompt }
            },
            max_tokens = 200,
            temperature = 0.3
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return string.IsNullOrWhiteSpace(text)
            ? anomaly.Summary
            : text.Trim();
    }

    private static string BuildPrompt(Anomaly anomaly, MarketTick? tick)
    {
        var price = tick is null ? "unknown" : tick.Price.ToString("0.00");
        var volume = tick is null ? "unknown" : tick.Volume.ToString("N0");
        return $"Anomaly type: {anomaly.Type}. Ticker: {anomaly.Ticker}. " +
               $"Summary: {anomaly.Summary} Latest price: {price}. Latest volume: {volume}. " +
               "Explain in plain English what this kind of event usually means for a trader, and what to watch next.";
    }
}
