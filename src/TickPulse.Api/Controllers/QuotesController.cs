using Microsoft.AspNetCore.Mvc;
using TickPulse.Api.Services;

namespace TickPulse.Api.Controllers;

[ApiController]
[Route("api/quotes")]
public sealed class QuotesController : ControllerBase
{
    private readonly LatestQuotes _latest;

    public QuotesController(LatestQuotes latest)
    {
        _latest = latest;
    }

    [HttpGet]
    public IActionResult GetQuotes()
    {
        return Ok(_latest.GetAll());
    }

    [HttpGet("{ticker}")]
    public IActionResult GetQuote(string ticker)
    {
        var quote = _latest.Get(ticker.ToUpperInvariant());
        return quote is null ? NotFound(new { error = $"No quote yet for {ticker}." }) : Ok(quote);
    }
}
