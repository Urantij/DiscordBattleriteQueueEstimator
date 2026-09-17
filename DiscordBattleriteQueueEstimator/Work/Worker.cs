using System.Text.RegularExpressions;
using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Discord;
using DiscordBattleriteQueueEstimator.Shared.Data.Models;
using DiscordBattleriteQueueEstimator.Utils;

namespace DiscordBattleriteQueueEstimator.Work;

public class NewMatchStatusData(
    OnlineUser onlineUser,
    int teamSize,
    string hero,
    int score1,
    int score2,
    int bo,
    int partySize,
    DateTimeOffset date)
{
    public OnlineUser OnlineUser { get; } = onlineUser;

    public int TeamSize { get; } = teamSize;

    public string Hero { get; } = hero;

    public int Score1 { get; } = score1;
    public int Score2 { get; } = score2;
    public int Bo { get; } = bo;

    public int PartySize { get; } = partySize;

    public DateTimeOffset Date { get; } = date;
}

public class NewGenericStatusData(OnlineUser onlineUser, string? details, DateTimeOffset date)
{
    public OnlineUser OnlineUser { get; } = onlineUser;

    public string? Details { get; } = details;

    public DateTimeOffset Date { get; } = date;
}

public partial class Worker : IHostedService
{
    private readonly Database _database;
    private readonly ILogger<Worker> _logger;

    private readonly List<OnlineUser> _users = new();

    private readonly SoloLooper _looper;

    public event Action<NewMatchStatusData>? NewMatchStatusArrived;
    public event Action<NewGenericStatusData>? NewGenericStatusArrived;

    public Worker(Discorb discorb, Database database, ILogger<Worker> logger)
    {
        _database = database;
        _logger = logger;

        _looper = new SoloLooper(_logger);

        discorb.UserRped += DiscorbOnUserRped;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        int? lastStatusId = await _database.GetLastStatusIdAsync();

        if (lastStatusId != null)
        {
            int? lastClearPointStatus = await _database.GetLastPointStatusAsync();

            if (lastStatusId == lastClearPointStatus)
                return;

            await _database.CreateLastPointAsync(lastStatusId.Value, DateTimeOffset.UtcNow);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _looper.Stop();

        return Task.CompletedTask;
    }

    public int CountUsers()
    {
        lock (_users)
        {
            return _users.Count;
        }
    }

    public int CountUsers(Func<OnlineUser, bool> predicate)
    {
        lock (_users)
        {
            return _users.Where(predicate).Count();
        }
    }

    public List<OnlineUser> GetUsers()
    {
        lock (_users)
        {
            return _users.ToList();
        }
    }

    private void DiscorbOnUserRped(UserInfo obj)
    {
        _looper.Add(() => ProcessAsync(obj));
    }

    private async Task ProcessAsync(UserInfo userNewInfo)
    {
        Match? matchParsedRegex = null;
        if (userNewInfo.Info?.Details != null)
        {
            matchParsedRegex = RpDetails.ArenaRegex.Match(userNewInfo.Info.Details);
            if (matchParsedRegex.Success)
            {
                // Такая багулина бывает и забивает логи. Скипаем.
                if (matchParsedRegex.Groups["Bo"].Value == "0")
                    return;
            }
        }

        OnlineUser? user;
        lock (_users)
            user = _users.FirstOrDefault(u => u.User.DiscordId == userNewInfo.Id);

        bool needStatusInsert = false;
        if (user == null)
        {
            // Нет смысла записывать FakeRp.
            // Он нужен только, чтобы убедиться в наличии какого-то события.
            // То есть он нужен, только если проверять длину события ДО него.
            if (userNewInfo.Info == null || userNewInfo.FakeRp)
                return;

            DbUser? dbUser = await _database.LoadUserAsync(userNewInfo.Id);
            if (dbUser == null)
            {
                dbUser = await _database.CreateUserAsync(userNewInfo.Id);
                _logger.LogDebug("Создали пользователя {id}", dbUser.Id);
            }
            else
            {
                _logger.LogDebug("Загрузили пользователя {id}", dbUser.Id);
            }

            user = new OnlineUser(dbUser, userNewInfo.FakeRp ? null : userNewInfo.Info);
            lock (_users)
                _users.Add(user);

            needStatusInsert = true;
        }
        else if (userNewInfo.Info == null)
        {
            if (userNewInfo.FakeRp)
            {
                needStatusInsert = !user.IsLastRpFake();
            }
            else
            {
                lock (_users)
                    _users.Remove(user);

                needStatusInsert = true;
            }
        }
        else if (user.GetLast() != userNewInfo.Info)
        {
            user.Add(userNewInfo.Info);
            needStatusInsert = true;
        }

        if (!needStatusInsert)
            return;

        Task dbTask = _database.InsertStatusAsync(user.User.Id, userNewInfo.FakeRp, userNewInfo.Info,
            userNewInfo.Date);

        // int.Parse безопасно потому что регекс чекает на цифры. да в ТЕОРИИ там может быть странный набор цифр, но я не верю.
        // херо и патисайз там всегда есть, но чтобы иде не плакала
        if (matchParsedRegex?.Success == true && userNewInfo.Info?.Hero != null)
        {
            NewMatchStatusArrived?.Invoke(new NewMatchStatusData(user,
                int.Parse(matchParsedRegex.Groups["team1"]
                    .Value), // ну они же никогда не будут разными в тим1 и 2 да да да
                userNewInfo.Info.Hero,
                int.Parse(matchParsedRegex.Groups["score1"].Value),
                int.Parse(matchParsedRegex.Groups["score2"].Value),
                int.Parse(matchParsedRegex.Groups["Bo"].Value),
                userNewInfo.Info.PartySize ?? -1,
                userNewInfo.Date));
        }
        else
        {
            NewGenericStatusArrived?.Invoke(new NewGenericStatusData(user, userNewInfo.Info?.Details,
                userNewInfo.Date));
        }

        await dbTask;
        _logger.LogDebug("Записали статус пользователя {id} {isFake} {status}", user.User.Id, userNewInfo.FakeRp,
            userNewInfo.Info);
    }
}