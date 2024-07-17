using System.ComponentModel.DataAnnotations;

namespace DiscordBattleriteQueueEstimator.Data.Models;

public class DbUser
{
    [Key]
    public int Id { get; set; }
    
    public ulong DiscordId { get; set; }
    
    public ICollection<DbUserStatus> Statuses { get; set; }
}