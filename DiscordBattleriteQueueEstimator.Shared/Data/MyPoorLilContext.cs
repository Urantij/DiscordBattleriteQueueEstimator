using DiscordBattleriteQueueEstimator.Shared.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DiscordBattleriteQueueEstimator.Shared.Data;

public class MyPoorLilContext : DbContext
{
    public DbSet<DbUser> Users { get; set; }
    public DbSet<DbUserStatus> Statuses { get; set; }
    public DbSet<DbClearPoint> Points { get; set; }
    public DbSet<DbUserMatch> UserMatches { get; set; }

    public MyPoorLilContext()
    {
    }

    public MyPoorLilContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=db.sqlite;");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbUserStatus>()
            .OwnsOne(e => e.RpInfo);

        modelBuilder.Entity<DbUser>()
            .OwnsMany(u => u.Matches)
            .WithOwner(m => m.User);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new UlongConvection());

        configurationBuilder.Properties<DateTime>()
            .HaveConversion<DateTimeToBinaryConverter>();
        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<DateTimeToBinaryConverter>();

        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}