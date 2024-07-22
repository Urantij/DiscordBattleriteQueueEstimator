using System.Text.RegularExpressions;
using DiscordBattleriteQueueEstimator.Data;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;

namespace DiscordBattleriteQueueEstimator.Discord.Commands;

public class HeroCommand : BaseCommand
{
    class UserStatusDTO
    {
        public DateTimeOffset Date { get; init; }
        public bool FakeRp { get; init; }
        public string? Details { get; init; }
        public string? Hero { get; init; }
    }

    private readonly Database _database;

    public HeroCommand(Database database, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _database = database;
    }

    public override async Task DoAsync(DiscordClient sender, InteractionCreateEventArgs args)
    {
        await using MyContext context = await _database.CreateContextAsync();

        int? userId = await context.Users
            .Where(u => u.DiscordId == args.Interaction.User.Id)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        if (userId == null)
        {
            await SendReplyAsync(args, "noob");
            return;
        }

        UserStatusDTO[] statuses = await context.Statuses
            .OrderBy(s => s.Date)
            .Where(s => s.UserId == userId)
            .Select(s => new UserStatusDTO()
            {
                Date = s.Date,
                FakeRp = s.FakeRp,
                Details = s.RpInfo!.Details,
                Hero = s.RpInfo!.Hero
            }).ToArrayAsync();

        DateTimeOffset?[] points = await context.Points
            .OrderBy(p => p.Date)
            .Select(p => (DateTimeOffset?)p.Date)
            .ToArrayAsync();

        Dictionary<string, TimeSpan> heroDict = new();

        int currentIndex = 0;
        while (currentIndex < statuses.Length)
        {
            UserStatusDTO status = statuses[currentIndex];

            if (status.Details == null || status.FakeRp)
            {
                currentIndex++;
                continue;
            }

            DateTimeOffset? nextPoint = points.FirstOrDefault(p => p > status.Date);

            if (currentIndex == statuses.Length - 1)
                break;

            UserStatusDTO nextStatus = statuses[currentIndex + 1];

            if (nextPoint < nextStatus.Date)
            {
                currentIndex++;
                continue;
            }

            TimeSpan diff = nextStatus.Date - status.Date;

            Match match = Work.Worker.ArenaRegex.Match(status.Details);
            if (!match.Success)
            {
                currentIndex++;
                continue;
            }

            int lookBehindIndex = currentIndex - 1;
            if (lookBehindIndex < 0)
            {
                currentIndex++;
                continue;
            }

            string? hero = status.Hero;

            int lookUpIndex = currentIndex + 1;
            DateTimeOffset? postGameDate = null;
            while (lookUpIndex < statuses.Length)
            {
                UserStatusDTO lookUpStatus = statuses[lookUpIndex];

                if (nextPoint < lookUpStatus.Date)
                    break;

                if (lookUpStatus.FakeRp)
                {
                    postGameDate = lookUpStatus.Date;
                    lookUpIndex++;
                    break;
                }

                if (hero == null)
                {
                    hero = lookUpStatus.Hero;
                }
                else if (hero != lookUpStatus.Hero && lookUpStatus.Hero != null)
                {
                    // чето странное
                    break;
                }

                postGameDate = lookUpStatus.Date;

                if (lookUpStatus.Details != null && Work.Worker.ArenaRegex.IsMatch(lookUpStatus.Details))
                    lookUpIndex++;
                else
                    break;
            }

            if (lookUpIndex == statuses.Length)
            {
                postGameDate = DateTimeOffset.UtcNow;
            }

            currentIndex = lookUpIndex - 1;

            if (postGameDate != null)
            {
                diff = postGameDate.Value - status.Date;
            }

            if (hero == null)
            {
                currentIndex++;
                continue;
            }

            if (!heroDict.TryGetValue(hero, out TimeSpan currentHeroTime))
            {
                heroDict[hero] = diff;
            }
            else
            {
                heroDict[hero] = currentHeroTime + diff;
            }

            currentIndex++;
        }

        if (heroDict.Count > 0)
        {
            string result = string.Join('\n',
                heroDict.OrderByDescending(p => p.Value)
                    .Select(p => $"{p.Key}: {TimeCommand.MakeTime(p.Value)}")
                    .ToArray());

            await SendReplyAsync(args, result);
        }
        else
        {
            await SendReplyAsync(args, "i dont know you, you dont belong here.");
        }
    }
}