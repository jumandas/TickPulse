using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TickPulse.Api.Data;
using TickPulse.Core;

namespace TickPulse.Api.Controllers;

[ApiController]
[Route("api/watchlist")]
public sealed class WatchlistController : ControllerBase
{
    private readonly TickPulseDbContext _db;

    public WatchlistController(TickPulseDbContext db)
    {
        _db = db;
    }

    public sealed record AddToWatchlistRequest(string Ticker);

    [HttpGet]
    public async Task<IActionResult> GetWatchlist(CancellationToken cancellationToken)
    {
        var items = await _db.WatchlistItems.OrderBy(w => w.Ticker).ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> AddToWatchlist([FromBody] AddToWatchlistRequest request, CancellationToken cancellationToken)
    {
        var ticker = request.Ticker?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return BadRequest(new { error = "Ticker is required." });
        }

        var symbolExists = await _db.Symbols.AnyAsync(s => s.Ticker == ticker, cancellationToken);
        if (!symbolExists)
        {
            return NotFound(new { error = $"Unknown symbol {ticker}." });
        }

        var existing = await _db.WatchlistItems.FirstOrDefaultAsync(w => w.Ticker == ticker, cancellationToken);
        if (existing is not null)
        {
            return Ok(existing);
        }

        var item = new WatchlistItem { Ticker = ticker, AddedAt = DateTimeOffset.UtcNow };
        _db.WatchlistItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetWatchlist), new { }, item);
    }

    [HttpDelete("{ticker}")]
    public async Task<IActionResult> RemoveFromWatchlist(string ticker, CancellationToken cancellationToken)
    {
        var upper = ticker.ToUpperInvariant();
        var item = await _db.WatchlistItems.FirstOrDefaultAsync(w => w.Ticker == upper, cancellationToken);
        if (item is null)
        {
            return NotFound(new { error = $"Symbol {ticker} is not on the watchlist." });
        }

        _db.WatchlistItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
