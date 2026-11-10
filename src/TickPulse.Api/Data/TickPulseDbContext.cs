using Microsoft.EntityFrameworkCore;
using TickPulse.Core;

namespace TickPulse.Api.Data;

// EF Core context. Ticks and anomaly events are append only history.
// Symbols are seeded from configuration. Watchlist items are user data.
public sealed class TickPulseDbContext : DbContext
{
    public TickPulseDbContext(DbContextOptions<TickPulseDbContext> options)
        : base(options)
    {
    }

    public DbSet<MarketTick> Ticks => Set<MarketTick>();
    public DbSet<Symbol> Symbols => Set<Symbol>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<AnomalyEventRecord> AnomalyEvents => Set<AnomalyEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MarketTick>(entity =>
        {
            entity.HasKey(t => new { t.Ticker, t.Timestamp });
            entity.Property(t => t.Price).HasColumnType("decimal(18,2)");
            entity.Property(t => t.ChangePercent).HasColumnType("decimal(10,4)");
            entity.HasIndex(t => t.Timestamp);
        });

        modelBuilder.Entity<Symbol>(entity =>
        {
            entity.HasKey(s => s.Ticker);
            entity.Property(s => s.BasePrice).HasColumnType("decimal(18,2)");
            entity.Property(s => s.Volatility).HasColumnType("decimal(10,6)");
        });

        modelBuilder.Entity<WatchlistItem>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.HasIndex(w => w.Ticker).IsUnique();
        });

        modelBuilder.Entity<AnomalyEventRecord>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.ChangePercent).HasColumnType("decimal(10,4)");
            entity.HasIndex(a => a.DetectedAt);
        });
    }
}

// One analyst event stored in the database, with its explanation.
public sealed class AnomalyEventRecord
{
    public int Id { get; set; }
    public required string Ticker { get; set; }
    public required AnomalyType Type { get; set; }
    public decimal ChangePercent { get; set; }
    public required DateTimeOffset DetectedAt { get; set; }
    public required string Summary { get; set; }
    public required string Explanation { get; set; }
    public required string AnalystName { get; set; }
}
