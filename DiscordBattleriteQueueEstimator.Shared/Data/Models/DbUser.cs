using System.ComponentModel.DataAnnotations;

namespace DiscordBattleriteQueueEstimator.Shared.Data.Models;

public class DbUser
{
    [Key] public int Id { get; set; }

    public ulong DiscordId { get; set; }

    public ICollection<DbUserStatus> Statuses { get; set; }
    public ICollection<DbUserMatch> Matches { get; set; }
}