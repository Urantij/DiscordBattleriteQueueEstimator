using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Shared.Data.Models;
using DiscordBattleriteQueueEstimator.Utils;

namespace DiscordBattleriteQueueEstimator.Work;

class TrackedMatch
{
    public DbUserMatch UserMatch { get; }

    public DbMatchType MatchType { get; }

    public string Hero { get; set; }

    public int LastScore1 { get; set; }
    public int LastScore2 { get; set; }
    public int Bo { get; set; }

    public DateTimeOffset StartDate { get; }
    public DateTimeOffset? EndDate { get; set; }

    public TrackedMatch(DbUserMatch userMatch, DbMatchType matchType, string hero, int lastScore1, int lastScore2,
        int bo,
        DateTimeOffset startDate)
    {
        UserMatch = userMatch;
        MatchType = matchType;
        Hero = hero;
        LastScore1 = lastScore1;
        LastScore2 = lastScore2;
        Bo = bo;
        StartDate = startDate;
    }
}

// из за работы с бд, нужно обрабатывать вещи в очереди. в теории самый первый таск с матчем может писать в бд, и тут прилетает второй и всё становится грустно.
// в теории можно делать по таску на каждый матч, но зачем в целом не сильно понятно...
// типа если будет большой поток, тогда да, луперы должны быть в матче, но пока похуй.

public class MatchObserver : IHostedService
{
    // TODO короч перезапуск бота посреди чьего то матча этот матч раздвоит в памяти, но хызы, впадлу думать

    private readonly Worker _worker;
    private readonly Database _database;
    private readonly ILogger<MatchObserver> _logger;

    private readonly TimeSpan _healthCheckCd = TimeSpan.FromHours(2);
    private readonly TimeSpan _matchHealthDuration = TimeSpan.FromHours(2);

    // dbuser.id
    private readonly Dictionary<int, TrackedMatch> _dictionary = new();

    private readonly SoloLooper _looper;

    private DateTimeOffset _lastHealthCheck = DateTimeOffset.UtcNow;

    public MatchObserver(Worker worker, Database database, ILogger<MatchObserver> logger)
    {
        _worker = worker;
        _database = database;
        _logger = logger;

        _looper = new SoloLooper(logger);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _worker.NewMatchStatusArrived += WorkerOnNewMatchStatusArrived;
        _worker.NewGenericStatusArrived += WorkerOnNewGenericStatusArrived;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _looper.Stop();

        _worker.NewMatchStatusArrived -= WorkerOnNewMatchStatusArrived;
        _worker.NewGenericStatusArrived -= WorkerOnNewGenericStatusArrived;

        return Task.CompletedTask;
    }

    private void WorkerOnNewMatchStatusArrived(NewMatchStatusData obj)
    {
        _looper.Add(async () =>
        {
            HealthCheck();

            if (!_dictionary.TryGetValue(obj.OnlineUser.User.Id, out TrackedMatch? trackedMatch))
            {
                DbMatchType matchType = DbMatchType.Unknown;

                // иногда "In Menus" проскакивает между очередью и матчем
                // один раз после лобака проскочило "In Custom Match" между ояередью и и матчем
                foreach (RpInfo? prevInfo in obj.OnlineUser.GetList().AsEnumerable().Reverse().Skip(1))
                {
                    // TODO если так подумать, какой нить вход в кастомки не ловится и всё может портить. наверное стоит сравнивать время, вряд ли там больше секунды
                    // и вообще в кастомках не рисует ни героя ни счёт ничего. так что мы всегда в мме если находим матчевую строку

                    if (prevInfo?.Details == RpDetails.CasualQueue)
                    {
                        matchType = DbMatchType.Casual;
                        break;
                    }
                    else if (prevInfo?.Details == RpDetails.LeagueQueue)
                    {
                        matchType = DbMatchType.League;
                        break;
                    }
                }

                // TODO в теории если бота врубили посреди матча, и первый статус он получает кривой, всё будет плохо... типа можно ждать 1 сек, вдруг придёт обнова, 
                // но ситуация редкая и мне впадлу

                DbUserMatch db = await _database.CreateUserMatchAsync(obj.OnlineUser.User.Id, matchType, obj.Hero,
                    obj.PartySize, obj.Score1, obj.Score2, obj.Date);

                trackedMatch = new TrackedMatch(db, matchType, obj.Hero, obj.Score1, obj.Score2, obj.Bo,
                    obj.Date);

                _dictionary[obj.OnlineUser.User.Id] = trackedMatch;
                return;
            }

            // если пришёл статус о матче, когда мы думаем, что матч закончился, случилось чето нехорошее. просто сделаем вид, что мы ниче не видели и не знаем.
            if (trackedMatch.EndDate != null)
                return;

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

            // то есть первый раз он пишет правду, на это и можно ориентироваться.

            // TODO как то дебильно если и скор и герой меняется, но по идее это невозможно и мне впадлу думать в аоте над этим

            if (obj.Hero != trackedMatch.Hero)
            {
                // героя показывает даже в очереди, а в лиге героя можно репикать)
                trackedMatch.Hero = obj.Hero;

                await _database.UpdateUserMatchHeroAsync(trackedMatch.UserMatch.Id, trackedMatch.Hero);
            }

            if (obj.Score1 == trackedMatch.LastScore1 && obj.Score2 == trackedMatch.LastScore2)
                return;
            if (obj.Score1 < trackedMatch.LastScore1 || obj.Score2 < trackedMatch.LastScore2)
                return;

            trackedMatch.LastScore1 = obj.Score1;
            trackedMatch.LastScore2 = obj.Score2;

            int maxScore = (trackedMatch.Bo + 1) / 2;

            if (obj.Score1 >= maxScore || obj.Score2 >= maxScore)
            {
                trackedMatch.EndDate = obj.Date;
                await _database.UpdateUserMatchFinishAsync(trackedMatch.UserMatch.Id, trackedMatch.LastScore1,
                    trackedMatch.LastScore2, trackedMatch.EndDate.Value);

                // судя по всему, статусов об этом матче не будет больше,
                // но на всякий случай, я запомню ненадолго этот матс, чтобы он не начался второй раз.
                // вообще после матча всегда будет либо меню,
                // если чел вышел в меню, либо пустой статус, если чел вышел из игры. да?
                // _dictionary.Remove(obj.OnlineUser.User.Id);
                // так что будем убирать в женерике
            }
            else
            {
                await _database.UpdateUserMatchScoreAsync(trackedMatch.UserMatch.Id, trackedMatch.LastScore1,
                    trackedMatch.LastScore2);
            }
        });
    }

    private void WorkerOnNewGenericStatusArrived(NewGenericStatusData obj)
    {
        _looper.Add(() =>
        {
            HealthCheck();

            // ниче не знаем значит и делать нечего.
            if (!_dictionary.TryGetValue(obj.OnlineUser.User.Id, out TrackedMatch? match))
                return Task.CompletedTask;

            if (match.EndDate != null)
            {
                // если матч закончился, мы уберём его из памяти, чтобы следующий матч мог появиться
                // тут неважно, что пришло в статусе - чел вне игры. чел в меню или очереди. любой статус уже говорит, что матч не вернётся.
                _dictionary.Remove(match.UserMatch.User.Id);
                return Task.CompletedTask;
            }

            // Если матч не завершился на наших глазах, всё плохо. Тому что тут непонятно, чел вылетел, чел ливнул, или че ваще
            // Точно можно будет сказать, что матч сдох для нас, если чел окажется в очереди или в лобби.
            // возьму матч вместо лобби, так как хызы, можно ли зайти в лобби или нельзя

            if (obj.Details is not (RpDetails.CustomMatch or RpDetails.CasualQueue or RpDetails.LeagueQueue))
            {
                return Task.CompletedTask;
            }

            // TODO может быть как то надо отмечать в бд что мы ниче не знаем, хэ зэ

            _dictionary.Remove(match.UserMatch.User.Id);

            return Task.CompletedTask;
        });
    }

    private void HealthCheck()
    {
        if (DateTimeOffset.UtcNow - _lastHealthCheck < _healthCheckCd)
            return;

        _lastHealthCheck = DateTimeOffset.UtcNow;

        List<int>? removers = null;
        foreach (KeyValuePair<int, TrackedMatch> pairs in _dictionary)
        {
            if (DateTimeOffset.UtcNow - pairs.Value.StartDate < _matchHealthDuration)
                continue;

            removers ??= new List<int>();
            removers.Add(pairs.Key);
        }

        if (removers != null)
        {
            foreach (int key in removers)
            {
                _dictionary.Remove(key);
            }
        }

        _logger.LogDebug("Health removers {count}", removers?.Count ?? 0);
    }
}