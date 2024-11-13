using System.ComponentModel.DataAnnotations;

namespace DiscordBattleriteQueueEstimator.Data;

public class DbUser
{
    [Key]
    public int Id { get; set; }
    
    public ulong DiscordId { get; set; }
    
    public ICollection<DbUserStatus> Statuses { get; set; }
}