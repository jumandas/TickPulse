using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TickPulse.Api.Data;
using TickPulse.Core;

namespace TickPulse.Api.Controllers;

[ApiController]
[Route("api/symbols")]
public sealed class SymbolsController : ControllerBase
{
    private readonly TickPulseDbContext _db;
    private readonly IConfiguration _configuration;

    public SymbolsController(TickPulseDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetSymbols(CancellationToken cancellationToken)
    {
        try
        {
            var symbols = await _db.Symbols.OrderBy(s => s.Ticker).ToListAsync(cancellationToken);
            return Ok(symbols);
        }
        catch (Exception)
        {
            var fallback = _configuration.GetSection("TickEngine:Symbols").Get<List<Symbol>>() ?? new List<Symbol>();
            return Ok(fallback);
        }
    }

    [HttpGet("{ticker}")]
    public async Task<IActionResult> GetSymbol(string ticker, CancellationToken cancellationToken)
    {
        var symbol = await _db.Symbols.FindAsync(new object[] { ticker.ToUpperInvariant() }, cancellationToken);
        return symbol is null ? NotFound(new { error = $"Unknown symbol {ticker}." }) : Ok(symbol);
    }
}
