using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TickPulse.Api.Data;

namespace TickPulse.Api.Controllers;

[ApiController]
[Route("api/history")]
public sealed class HistoryController : ControllerBase
{
    private readonly TickPulseDbContext _db;

    public HistoryController(TickPulseDbContext db)
    {
        _db = db;
    }

    [HttpGet("{ticker}")]
    public async Task<IActionResult> GetHistory(string ticker, [FromQuery] int minutes = 60, [FromQuery] int limit = 500, CancellationToken cancellationToken = default)
    {
        if (minutes < 1 || minutes > 1440)
        {
            return BadRequest(new { error = "minutes must be between 1 and 1440." });
        }

        if (limit < 1 || limit > 5000)
        {
            return BadRequest(new { error = "limit must be between 1 and 5000." });
        }

        var since = DateTimeOffset.UtcNow.AddMinutes(-minutes);
        var upper = ticker.ToUpperInvariant();

        var ticks = await _db.Ticks
            .Where(t => t.Ticker == upper && t.Timestamp >= since)
            .OrderBy(t => t.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Ok(ticks);
    }
}
