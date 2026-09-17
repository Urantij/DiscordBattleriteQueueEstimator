using System.Text.RegularExpressions;
using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Shared.Data.Models;
using DiscordBattleriteQueueEstimator.Utils;
using Microsoft.EntityFrameworkCore;

namespace DiscordBattleriteQueueEstimator.Work;

class BasePoint
{
    public DateTimeOffset Date { get; }

    public BasePoint(DateTimeOffset date)
    {
        Date = date;
    }
}

class CasualQueuePoint : BasePoint
{
    public CasualQueuePoint(DateTimeOffset date) : base(date)
    {
    }
}

class LeagueQueuePoint : BasePoint
{
    public LeagueQueuePoint(DateTimeOffset date) : base(date)
    {
    }
}

class CustomLobbyPoint : BasePoint
{
    public CustomLobbyPoint(DateTimeOffset date) : base(date)
    {
    }
}

class CustomMatchPoint : BasePoint
{
    public CustomMatchPoint(DateTimeOffset date) : base(date)
    {
    }
}

class BrawlPoint : BasePoint
{
    public BrawlPoint(DateTimeOffset date) : base(date)
    {
    }
}

class PlaygroundPoint : BasePoint
{
    public PlaygroundPoint(DateTimeOffset date) : base(date)
    {
    }
}

class MatchPoint : BasePoint
{
    public bool Saved { get; set; } = false;

    public int TeamSize { get; set; }
    public int PartySize { get; set; }

    public int Score1 { get; set; }
    public int Score2 { get; set; }
    public int Bo { get; set; }

    public bool? Win { get; set; } = null;

    public DateTimeOffset LastDate { get; set; }

    public MatchPoint(DateTimeOffset date) : base(date)
    {
        LastDate = date;
    }
}

// переводит весь массив статусов в информацию о них
public static class StatusReprocessor
{
    public static async Task DoAsync(Database database)
    {
        await using var context = await database.CreateContextAsync();

        IOrderedQueryable<DbUser> query = context.Users.AsNoTracking().OrderBy(s => s.Id);

        await foreach (var user in query.AsAsyncEnumerable())
        {
            await ProcessUserAsync(user, database);
        }
    }

    private static async Task ProcessUserAsync(DbUser user, Database database)
    {
        // я не хочу доделывать, это нервно

        await using var context = await database.CreateContextAsync();

        // просто будем идти сверху вниз по всем статусам
        // юзается та же логика, что и в текущем определении матчей. находим очередь все дела.
        // тока ещё клир точки нужно учитывать блееее

        DbClearPoint[] clearPoints = await context.Points.OrderBy(p => p.Date).ToArrayAsync();

        IOrderedQueryable<DbUserStatus> query = context.Statuses.AsNoTracking()
            .Where(status => status.UserId == user.Id)
            .OrderBy(s => s.Id);

        int count = await context.Statuses.Where(status => status.UserId == user.Id).CountAsync();

        BasePoint? lastPoint = null;

        int counter = -1;
        await foreach (var status in query.AsAsyncEnumerable())
        {
            counter++;

            again: ;

            if (lastPoint != null && clearPoints.Any(p => p.Date > lastPoint.Date && p.Date < status.Date))
            {
                // завершить ласт, начать новый 
                if (lastPoint is MatchPoint lostPoint)
                {
                    await SaveMatchAsync(lostPoint, database);
                }

                lastPoint = null;
                goto again;
            }

            if (lastPoint is MatchPoint match)
            {
                if (status.RpInfo?.Details == null)
                {
                    // похуй
                    continue;
                }

                // даааа, в теории во время матча пойдёт время меню, если чел перезаходит, но я ебал.

                Match matchParsedRegex = RpDetails.ArenaRegex.Match(status.RpInfo.Details);
                if (matchParsedRegex.Success)
                {
                    // Такая багулина бывает и забивает логи. Скипаем.
                    if (matchParsedRegex.Groups["Bo"].Value == "0")
                        continue;

                    int s1 = int.Parse(matchParsedRegex.Groups["score1"].Value);
                    int s2 = int.Parse(matchParsedRegex.Groups["score2"].Value);
                    int bo = int.Parse(matchParsedRegex.Groups["Bo"].Value);

                    if (s1 < match.Score1 || s2 < match.Score2)
                        continue;

                    match.LastDate = status.Date;

                    match.Score1 = s1;
                    match.Score2 = s2;
                    match.Bo = bo;

                    int maxScore = (bo + 1) / 2;

                    if (s1 >= maxScore || s2 >= maxScore)
                    {
                        match.Win = s1 > s2;

                        await SaveMatchAsync(match, database);
                    }
                }
                else if (status.RpInfo.Details == RpDetails.LeagueQueue)
                {
                    await SaveMatchAsync(match, database);

                    lastPoint = new LeagueQueuePoint(status.Date);
                }
                else if (status.RpInfo.Details == RpDetails.CasualQueue)
                {
                    await SaveMatchAsync(match, database);

                    lastPoint = new CasualQueuePoint(status.Date);
                }
                else if (status.RpInfo.Details == RpDetails.CustomLobby)
                {
                    await SaveMatchAsync(match, database);

                    lastPoint = new CustomLobbyPoint(status.Date);
                }
                else if (status.RpInfo.Details == RpDetails.CustomMatch)
                {
                    await SaveMatchAsync(match, database);

                    lastPoint = new CustomMatchPoint(status.Date);
                }
                else if (status.RpInfo.Details == RpDetails.Brawl)
                {
                    await SaveMatchAsync(match, database);

                    lastPoint = new CustomMatchPoint(status.Date);
                }
            }
        }
    }

    private static async Task SaveMatchAsync(MatchPoint matchPoint, Database database)
    {
        if (matchPoint.Saved)
            return;

        matchPoint.Saved = true;
    }
}