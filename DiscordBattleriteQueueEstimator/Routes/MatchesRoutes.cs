using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Shared.Data.Models;
using DiscordBattleriteQueueEstimator.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DiscordBattleriteQueueEstimator.Routes;

public class WebMatchModel(
    int id,
    DbMatchType matchType,
    string hero,
    int partySize,
    int score1,
    int score2,
    DateTime startDate,
    DateTime lastDate,
    bool? win)
{
    public int Id { get; } = id;

    public DbMatchType MatchType { get; } = matchType;

    public string Hero { get; } = hero;

    public int PartySize { get; } = partySize;

    public int Score1 { get; } = score1;
    public int Score2 { get; } = score2;

    public DateTime StartDate { get; } = startDate;
    public DateTime LastDate { get; } = lastDate;
    public bool? Win { get; } = win;
}

public static class MatchesRoutes
{
    private const int HardLimit = 100;

    private const string LimitQuery = "limit";
    private const string AfterQuery = "After";

    private static DateTimeOffset MakeDefaultAfter() => DateTimeOffset.UtcNow - TimeSpan.FromDays(100);

    public static async Task<IResult> GetAsync(HttpContext httpContext, [FromRoute] DiscordId id,
        [FromServices] Database database)
    {
        int limit = httpContext.Request.Query.GetInt(LimitQuery) ?? HardLimit;
        if (limit > HardLimit)
            limit = HardLimit;

        DateTimeOffset after = httpContext.Request.Query.GetDateTime(AfterQuery) ?? MakeDefaultAfter();
        long afterBinary = DateTimeOffsetToBinaryConverter.ToLong(after);

        ulong nid = id.Value;

        await using var dbContext = await database.CreateContextAsync();
        WebMatchModel[] result = await dbContext.UserMatches
            .Where(m => m.User.DiscordId == nid)
            // .Where(m => m.StartDate > after) // не работает в аот
            // .Where(m => EF.Property<long>(m, nameof(m.StartDate)) > afterBinary) // не работает в аот
            .Where(m => EF.Property<long>(m, "StartDate") > afterBinary)
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .Select(m =>
                new WebMatchModel(m.Id, m.MatchType, m.Hero, m.PartySize, m.Score1, m.Score2, m.StartDate.UtcDateTime,
                    m.LastDate.UtcDateTime, m.Win))
            .AsNoTracking()
            .ToArrayAsync();

        return TypedResults.Ok(result);
    }
}