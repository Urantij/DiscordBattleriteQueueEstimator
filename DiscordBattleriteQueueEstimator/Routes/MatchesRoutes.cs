using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Shared.Data.Models;
using DiscordBattleriteQueueEstimator.Utils;
using Microsoft.EntityFrameworkCore;

namespace DiscordBattleriteQueueEstimator.Routes;

public class WebMatchModel(
    DbMatchType matchType,
    string hero,
    int partySize,
    int score1,
    int score2,
    DateTimeOffset startDate,
    DateTimeOffset lastDate,
    bool? win)
{
    public DbMatchType MatchType { get; } = matchType;

    public string Hero { get; } = hero;

    public int PartySize { get; } = partySize;

    public int Score1 { get; } = score1;
    public int Score2 { get; } = score2;

    public DateTimeOffset StartDate { get; } = startDate;
    public DateTimeOffset LastDate { get; } = lastDate;
    public bool? Win { get; } = win;
}

public static class MatchesRoutes
{
    private const int HardLimit = 100;

    private const string LimitQuery = "limit";
    private const string AfterQuery = "After";

    private static DateTimeOffset MakeDefaultAfter() => DateTimeOffset.UtcNow - TimeSpan.FromDays(1);

    public static async Task<IResult> GetAsync(HttpContext httpContext, DiscordId id, Database database)
    {
        int limit = httpContext.Request.Query.GetInt(LimitQuery) ?? HardLimit;
        if (limit > HardLimit)
            limit = HardLimit;

        DateTimeOffset after = httpContext.Request.Query.GetDateTime(AfterQuery) ?? MakeDefaultAfter();

        await using var dbContext = await database.CreateContextAsync();
        WebMatchModel[] result = await dbContext.UserMatches
            .Where(m => m.User.DiscordId == id.Value)
            .Where(m => m.StartDate > after)
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .Select(m =>
                new WebMatchModel(m.MatchType, m.Hero, m.PartySize, m.Score1, m.Score2, m.StartDate, m.LastDate, m.Win))
            .AsNoTracking()
            .ToArrayAsync();

        return TypedResults.Ok(result);
    }
}