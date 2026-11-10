using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TickPulse.Analyst;
using TickPulse.Api.Data;

namespace TickPulse.Api.Controllers;

[ApiController]
[Route("api/analyst")]
public sealed class AnalystController : ControllerBase
{
    private readonly TickPulseDbContext _db;
    private readonly IMarketAnalyst _analyst;

    public AnalystController(TickPulseDbContext db, IMarketAnalyst analyst)
    {
        _db = db;
        _analyst = analyst;
    }

    // Which analyst is writing the explanations right now.
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new { analyst = _analyst.Name, note = _analyst.Name == "openai"
            ? "Explanations come from the OpenAI API."
            : "Explanations come from the built-in rule-based analyst. Set OPENAI_API_KEY to use the OpenAI analyst." });
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > 100)
        {
            return BadRequest(new { error = "limit must be between 1 and 100." });
        }

        var events = await _db.AnomalyEvents
            .OrderByDescending(a => a.DetectedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Ok(events);
    }
}
