using System.Text;
using System.Text.RegularExpressions;
using DiscordBattleriteQueueEstimator.Data;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;

namespace DiscordBattleriteQueueEstimator.Discord.Commands;

class UserStatusDTO
{
    public DateTimeOffset Date { get; init; }
    public bool FakeRp { get; init; }
    public string? Details { get; init; }
}

public class TimeCommand : BaseCommand
{
    private readonly Database _database;

    public TimeCommand(Database database, ILoggerFactory loggerFactory) : base(loggerFactory)
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
                Details = s.RpInfo.Details
            }).ToArrayAsync();

        DateTimeOffset?[] points = await context.Points
            .OrderBy(p => p.Date)
            .Select(p => (DateTimeOffset?)p.Date)
            .ToArrayAsync();

        TimeSpan menuTime = TimeSpan.Zero;
        TimeSpan leagueQueueTime = TimeSpan.Zero;
        TimeSpan casualQueueTime = TimeSpan.Zero;
        TimeSpan leagueArenaTime = TimeSpan.Zero;
        TimeSpan casualArenaTime = TimeSpan.Zero;
        TimeSpan unknownArenaTime = TimeSpan.Zero;

        int currentIndex = 0;
        while (currentIndex < statuses.Length)
        {
            UserStatusDTO status = statuses[currentIndex];

            if (status.Details == null || status.FakeRp)
            {
                currentIndex++;
                continue;
            }
            
            // Если точка раньше статуса, то между двумя статусами могло быть что угодно, это мы не рассчитываем.
            DateTimeOffset? nextPoint = points.FirstOrDefault(p => p > status.Date);

            if (currentIndex == statuses.Length - 1)
            {
                if (nextPoint != null)
                    break;
                
                TimeSpan diff2 = DateTimeOffset.UtcNow - status.Date;
                
                // Нужно бы придумать нормальную логику, но мне сложно
                // Алсо, если чел ща в матче и напишет, получается, нужно траверсить все рп игры до её начала...
                if (status.Details == "In Menus")
                    menuTime += diff2;
                else if (status.Details == "In Queue: League")
                    leagueQueueTime += diff2;
                else if (status.Details == "In Queue: Casual")
                    casualQueueTime += diff2;
                
                break;
            }

            UserStatusDTO nextStatus = statuses[currentIndex + 1];
            
            if (nextPoint < nextStatus.Date)
            {
                currentIndex++;
                continue;
            }

            TimeSpan diff = nextStatus.Date - status.Date;

            if (status.Details == "In Menus")
                menuTime += diff;
            else if (status.Details == "In Queue: League")
                leagueQueueTime += diff;
            else if (status.Details == "In Queue: Casual")
                casualQueueTime += diff;
            else
            {
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

                UserStatusDTO prevStatus = statuses[lookBehindIndex];

                // Мы ничего не знаем.
                if (prevStatus.FakeRp)
                {
                    currentIndex++;
                    continue;
                }

                // Иногда после нахождения игры приходит статус про меню. Нужно просто копнуть чуть дальше.
                if (prevStatus.Details == "In Menus")
                {
                    lookBehindIndex--;
                    if (lookBehindIndex < 0)
                    {
                        currentIndex++;
                        continue;
                    }

                    prevStatus = statuses[lookBehindIndex];
                    if (prevStatus.FakeRp)
                    {
                        currentIndex++;
                        continue;
                    }
                }

                // Если мы ща смотрим матч, то впереди куча весёлых рп о ходе этого матча.
                // Нужно идти вперёд до конца, учитывая точки и фейки.
                int lookUpIndex = currentIndex + 1;
                UserStatusDTO? postGameStatus = null;
                while (lookUpIndex < statuses.Length)
                {
                    UserStatusDTO lookUpStatus = statuses[lookUpIndex];
                    DateTimeOffset? lookUpPoint = points.FirstOrDefault(p => p > status.Date);

                    if (lookUpPoint < lookUpStatus.Date)
                    {
                        // Всё, больше мы ничего не узнаем.
                        currentIndex = lookUpIndex + 1;
                        break;
                    }

                    if (lookUpStatus.FakeRp)
                    {
                        // Мы не знаем, что там случилось, но мы знаем, что до этого момента была игра.
                        postGameStatus = lookUpStatus;
                        currentIndex = lookUpIndex + 1;
                        break;
                    }
                    
                    postGameStatus = lookUpStatus;

                    // Ещё можно проверять героя (учитывая, что во время загрузки матча первый статус приходит с нулл героем)
                    // И какой Bo в матче. Но впадлу как то.
                    if (Work.Worker.ArenaRegex.IsMatch(status.Details))
                        lookUpIndex++;
                    else
                        break;
                }

                if (postGameStatus != null)
                {
                    diff = postGameStatus.Date - status.Date;
                }

                // Возможно, стоит также добавить проверку на время. Вряд ли быстрый перескок статусы был дольше 10 секунд.
                if (prevStatus.Details == "In Queue: League")
                    leagueArenaTime += diff;
                else if (prevStatus.Details == "In Queue: Casual")
                    casualArenaTime += diff;
                else
                    unknownArenaTime += diff;
            }

            currentIndex++;
        }

        string times = FormTime(menuTime, leagueQueueTime, casualQueueTime, leagueArenaTime, casualArenaTime, unknownArenaTime);

        if (times.Length > 0)
        {
            await SendReplyAsync(args, times);
        }
        else
        {
            await SendReplyAsync(args, "i dont know you, you dont belong here.");   
        }
    }

    private static string FormTime(TimeSpan menuTime, TimeSpan leagueQueueTime, TimeSpan casualQueueTime, TimeSpan leagueArenaTime, TimeSpan casualArenaTime, TimeSpan unknownArenaTime)
    {
        StringBuilder b = new();

        if (menuTime.Ticks != 0)
            b.AppendLine($"Menu: {MakeTime(menuTime)}");
        if (leagueQueueTime.Ticks != 0)
            b.AppendLine($"League Queue: {MakeTime(leagueQueueTime)}");
        if (casualQueueTime.Ticks != 0)
            b.AppendLine($"Casual Queue: {MakeTime(casualQueueTime)}");
        if (leagueArenaTime.Ticks != 0)
            b.AppendLine($"League Arena Playtime: {MakeTime(leagueArenaTime)}");
        if (casualArenaTime.Ticks != 0)
            b.AppendLine($"Casual Arena Playtime: {MakeTime(casualArenaTime)}");
        if (unknownArenaTime.Ticks != 0)
            b.AppendLine($"Unknown Arena Playtime: {MakeTime(unknownArenaTime)}");

        return b.ToString();
    }

    private static string MakeTime(TimeSpan time)
    {
        if (time.TotalSeconds < 60)
            return $"{time.TotalSeconds:F0} Seconds";

        if (time.TotalMinutes < 60)
            return $"{time.TotalMinutes:F0} Minutes";

        return $"{time.TotalHours:F1} Hours";
    }
}