using System.Text.RegularExpressions;
using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Discord;
using DiscordBattleriteQueueEstimator.Shared.Data.Models;

namespace DiscordBattleriteQueueEstimator.Work;

public partial class Worker : IHostedService
{
    private readonly Database _database;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<Worker> _logger;

    private readonly List<OnlineUser> _users = new();

    private readonly Lock _loopLocker = new();
    private readonly Queue<UserInfo> _queue = new();
    private bool _looping = false;

    // "In 3v3 Arena | 0-0 | Bo5"
    public static readonly Regex ArenaRegex = MakeArenaRegex();

    public Worker(Discorb discorb, Database database, IHostApplicationLifetime lifetime, ILogger<Worker> logger)
    {
        _database = database;
        _lifetime = lifetime;
        _logger = logger;

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
        lock (_loopLocker)
        {
            _queue.Enqueue(obj);
            if (_looping)
                return;

            _looping = true;
            Task.Run(async () =>
            {
                try
                {
                    await LoopAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "loop сломався");
                }
            });
        }
    }

    private async Task LoopAsync()
    {
        while (!_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            UserInfo? userNewInfo;
            lock (_loopLocker)
            {
                if (!_queue.TryDequeue(out userNewInfo))
                {
                    _looping = false;
                    return;
                }
            }

            if (userNewInfo.Info?.Details != null)
            {
                Match match = ArenaRegex.Match(userNewInfo.Info.Details);
                if (match.Success)
                {
                    // Такая багулина бывает и забивает логи. Скипаем.
                    if (match.Groups["Bo"].Value == "0")
                        continue;
                }

                // Ещё иногда бывает, что в матче становится счёт 0-1, но клиент присылает 2 статуса
                // Сначала 1-0, а потом исправляет на 0-1. И так по паре раз за матч может быть.
                // Но это ловить мне впадлу.
                // Разница между ними может быть в 10 секунд лол, но может это я перезапускал, все другие в ту же секунду
                // При этом бывает, что оно 2 раза пишет криво и исправляет, а на третий уже нет
                // было ваще такое
                // Croak	In 3v3 Arena | 0-0 | Bo5	2026-09-10 15:39:49
                // Croak	In 3v3 Arena | 1-0 | Bo5	2026-09-10 15:42:46 это правда
                // Croak	In 3v3 Arena | 0-1 | Bo5	2026-09-10 15:42:57 ???
                // Croak	In 3v3 Arena | 1-0 | Bo5	2026-09-10 15:42:57 исправил
                // Croak	In 3v3 Arena | 2-0 | Bo5	2026-09-10 15:44:14 правда
                // Croak	In 3v3 Arena | 0-2 | Bo5	2026-09-10 15:44:25 ???
                // Croak	In 3v3 Arena | 2-0 | Bo5	2026-09-10 15:44:25 исправил
                // Croak	In 3v3 Arena | 2-1 | Bo5	2026-09-10 15:45:52 правда
                // Croak	In 3v3 Arena | 1-2 | Bo5	2026-09-10 15:46:03 ???
                // Croak	In 3v3 Arena | 2-1 | Bo5	2026-09-10 15:46:03 исправил
                // Croak	In 3v3 Arena | 2-2 | Bo5	2026-09-10 15:47:50 правда
                // Croak	In 3v3 Arena | 2-3 | Bo5	2026-09-10 15:49:56 правда
                // похоже он пишет правду сначала, потом иногда обсирается, тут же исправляет посреди раунда, а потом пишет правду, когда раунд действительно заканчивается
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
                    continue;

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

                user = new OnlineUser(dbUser, userNewInfo.FakeRp, userNewInfo.Info);
                lock (_users)
                    _users.Add(user);

                needStatusInsert = true;
            }
            else if (userNewInfo.Info == null)
            {
                if (userNewInfo.FakeRp)
                {
                    needStatusInsert = !user.LastRpFake;
                }
                else
                {
                    lock (_users)
                        _users.Remove(user);

                    needStatusInsert = true;
                }
            }
            else if (user.LastInfo != userNewInfo.Info)
            {
                user.LastInfo = userNewInfo.Info;
                needStatusInsert = true;
            }

            if (!needStatusInsert)
                continue;

            await _database.InsertStatusAsync(user.User.Id, userNewInfo.FakeRp, userNewInfo.Info,
                userNewInfo.Date);
            _logger.LogDebug("Записали статус пользователя {id} {isFake} {status}", user.User.Id, userNewInfo.FakeRp,
                userNewInfo.Info);
        }
    }

    [GeneratedRegex(@"In (?<team1>\d+)v(?<team2>\d+) Arena \| (?<score1>\d+)-(?<score2>\d+) \| Bo(?<Bo>\d+)",
        RegexOptions.Compiled)]
    private static partial Regex MakeArenaRegex();
}