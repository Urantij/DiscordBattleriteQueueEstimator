using System.Text.RegularExpressions;

namespace DiscordBattleriteQueueEstimator.Utils;

public static partial class RpDetails
{
    public const string Menu = "In Menus";
    public const string CasualQueue = "In Queue: Casual";
    public const string LeagueQueue = "In Queue: League";
    public const string CustomLobby = "In Custom Lobby";
    public const string CustomMatch = "In Custom Match";
    public const string WatchingReplay = "Watching Replay";
    public const string Brawl = "In Brawl";
    public const string Tutorial = "In Tutorial";
    public const string Playground = "In Playground";

    // In 3v3 Arena | 0-0 | Bo5
    // In 2v2 Arena | 0-0 | Bo5
    // VS AI | 0-0 | Bo3

    // "In 3v3 Arena | 0-0 | Bo5"
    public static readonly Regex ArenaRegex = MakeArenaRegex();

    [GeneratedRegex(@"In (?<team1>\d+)v(?<team2>\d+) Arena \| (?<score1>\d+)-(?<score2>\d+) \| Bo(?<Bo>\d+)",
        RegexOptions.Compiled)]
    private static partial Regex MakeArenaRegex();
}