using System.Text;
using System.Text.RegularExpressions;
using DiscordBattleriteQueueEstimator.Data;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;

namespace DiscordBattleriteQueueEstimator.Discord.Commands;

public class TimeCommand : BaseCommand
{
    class UserStatusDTO
    {
        public DateTimeOffset Date { get; init; }
        public bool FakeRp { get; init; }
        public string? Details { get; init; }
    }

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
                Details = s.RpInfo!.Details
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

                // Иногда во время загрузки приходит ивент типа
                // Hero = , PartySize = 1, Details = , State = Not in a group
                // Думаю, его следует просто игнорировать тут.
                if (string.IsNullOrEmpty(prevStatus.Details))
                {
                    lookBehindIndex--;
                    if (lookBehindIndex < 0)
                    {
                        currentIndex++;
                        continue;
                    }

                    prevStatus = statuses[lookBehindIndex];
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
                // Нужно ещё учитывать, что после всех этих пробежек, внешний цикл также шагнёт currentIndex++
                // То есть currentIndex после цикла должен быть равен индексу последнего статуса с ареной.
                // Ещё как то странно, что лукап это текущий индекс +1, и часть проверок на этот индекс уже сделаны, типа той же точет.
                // Ну да ладно.
                int lookUpIndex = currentIndex + 1;
                DateTimeOffset? postGameDate = null;
                while (lookUpIndex < statuses.Length)
                {
                    UserStatusDTO lookUpStatus = statuses[lookUpIndex];

                    if (nextPoint < lookUpStatus.Date)
                    {
                        // Всё, больше мы ничего не узнаем.
                        break;
                    }

                    if (lookUpStatus.FakeRp)
                    {
                        // Мы не знаем, что там случилось, но мы знаем, что до этого момента была игра.
                        postGameDate = lookUpStatus.Date;
                        // ++, Таким образом следующий цикл пропустит этот статус, так как он всё равно фейк и пофиг.
                        lookUpIndex++;
                        break;
                    }

                    postGameDate = lookUpStatus.Date;

                    // Ещё можно проверять героя (учитывая, что во время загрузки матча первый статус приходит с нулл героем)
                    // И какой Bo в матче. Но впадлу как то.
                    if (lookUpStatus.Details != null && Work.Worker.ArenaRegex.IsMatch(lookUpStatus.Details))
                        lookUpIndex++;
                    else
                        break;
                }

                // Если дошли до конца, значит это идёт текущий матч.
                if (lookUpIndex == statuses.Length)
                {
                    postGameDate = DateTimeOffset.UtcNow;
                }

                currentIndex = lookUpIndex - 1;

                if (postGameDate != null)
                {
                    diff = postGameDate.Value - status.Date;
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

        string times = FormTime(menuTime, leagueQueueTime, casualQueueTime, leagueArenaTime, casualArenaTime,
            unknownArenaTime);

        if (times.Length > 0)
        {
            await SendReplyAsync(args, times);
        }
        else
        {
            await SendReplyAsync(args, "i dont know you, you dont belong here.");
        }
    }

    private static string FormTime(TimeSpan menuTime, TimeSpan leagueQueueTime, TimeSpan casualQueueTime,
        TimeSpan leagueArenaTime, TimeSpan casualArenaTime, TimeSpan unknownArenaTime)
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

    public static string MakeTime(TimeSpan time)
    {
        if (time.TotalSeconds < 60)
            return $"{time.TotalSeconds:F0} Seconds";

        if (time.TotalMinutes < 60)
            return $"{time.TotalMinutes:F0} Minutes";

        return $"{time.TotalHours:F1} Hours";
    }
}