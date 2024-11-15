using DiscordBattleriteQueueEstimator.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DiscordBattleriteQueueEstimator.Data;

public class Database
{
    private readonly IDbContextFactory<MyContext> _factory;

    public Database(IDbContextFactory<MyContext> factory)
    {
        _factory = factory;
    }

    public Task<MyContext> CreateContextAsync()
    {
        return _factory.CreateDbContextAsync();
    }

    public async Task<DbUser> CreateUserAsync(ulong discordId)
    {
        await using MyContext context = await _factory.CreateDbContextAsync();

        DbUser user = new()
        {
            DiscordId = discordId
        };

        context.Users.Add(user);

        await context.SaveChangesAsync();

        return user;
    }

    public async Task<DbUser?> LoadUserAsync(ulong discordId)
    {
        ulong id = discordId;

        await using MyContext context = await _factory.CreateDbContextAsync();

        return await context.Users.FirstOrDefaultAsync(u => u.DiscordId == id);
    }

    public Task InsertStatusAsync(int userId, bool fakeRp, RpInfo? rpInfo, DateTimeOffset date)
    {
        DbUserStatus status = new()
        {
            UserId = userId,
            FakeRp = fakeRp,
            RpInfo = rpInfo,
            Date = date
        };
        return InsertStatusAsync(status);
    }

    public async Task InsertStatusAsync(DbUserStatus status)
    {
        await using MyContext context = await _factory.CreateDbContextAsync();

        context.Statuses.Add(status);

        await context.SaveChangesAsync();
    }

    public async Task<int?> GetLastStatusIdAsync()
    {
        await using MyContext context = await _factory.CreateDbContextAsync();

        return await context.Statuses
            .AsNoTrackingWithIdentityResolution()
            .OrderByDescending(s => s.Id)
            .Select(s => s.Id)
            .FirstOrDefaultAsync();
    }

    public async Task CreateLastPointAsync(int statusId, DateTimeOffset date)
    {
        await using MyContext context = await _factory.CreateDbContextAsync();

        context.Points.Add(new DbClearPoint()
        {
            StatusId = statusId,
            Date = date
        });

        await context.SaveChangesAsync();
    }

    public async Task<int?> GetLastPointStatusAsync()
    {
        await using MyContext context = await _factory.CreateDbContextAsync();

        return await context.Points
            .AsNoTrackingWithIdentityResolution()
            .OrderByDescending(s => s.Id)
            .Select(s => s.StatusId)
            .FirstOrDefaultAsync();
    }
}