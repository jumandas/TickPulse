using Microsoft.EntityFrameworkCore;
using TickPulse.Api.Data;
using TickPulse.Core;

namespace TickPulse.Api.Services;

// Seeds the synthetic symbols from configuration into the database on
// first startup. If a symbol already exists it is left alone.
public static class SymbolSeeder
{
    public static async Task SeedAsync(TickPulseDbContext db, IConfiguration configuration)
    {
        var symbols = configuration.GetSection("TickEngine:Symbols").Get<List<Symbol>>() ?? new List<Symbol>();
        if (symbols.Count == 0)
        {
            return;
        }

        foreach (var symbol in symbols)
        {
            var exists = await db.Symbols.AnyAsync(s => s.Ticker == symbol.Ticker);
            if (!exists)
            {
                db.Symbols.Add(symbol);
            }
        }

        await db.SaveChangesAsync();
    }
}
