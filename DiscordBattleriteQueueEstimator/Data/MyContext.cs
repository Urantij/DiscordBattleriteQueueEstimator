using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DiscordBattleriteQueueEstimator.Data;

public class MyContext : DbContext
{
    public DbSet<DbUser> Users { get; set; }
    public DbSet<DbUserStatus> Statuses { get; set; }
    public DbSet<DbClearPoint> Points { get; set; }

    public MyContext()
    {
    }

    public MyContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbUserStatus>()
            .OwnsOne(e => e.RpInfo);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

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