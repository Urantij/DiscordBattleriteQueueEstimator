using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordBattleriteQueueEstimator.Shared.Data.Models;

// технически 1 матч может быть несколькими матчами в бд потому что это скорее матч игрока чем матч
public class DbUserMatch
{
    [Key] public int Id { get; set; }

    [ForeignKey(nameof(User))] public int UserId { get; set; }
    public DbUser User { get; set; }

    public DbMatchType MatchType { get; set; }

    public string Hero { get; set; }

    public int PartySize { get; set; }

    public int Score1 { get; set; }
    public int Score2 { get; set; }

    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }

    public DbUserMatch()
    {
    }

    public DbUserMatch(int userId, DbMatchType matchType, string hero, int partySize, int score1, int score2,
        DateTimeOffset startDate)
    {
        UserId = userId;
        MatchType = matchType;
        Hero = hero;
        PartySize = partySize;
        Score1 = score1;
        Score2 = score2;
        StartDate = startDate;
    }
}