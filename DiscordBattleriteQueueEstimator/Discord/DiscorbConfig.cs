using System.Text.Json.Serialization;

namespace DiscordBattleriteQueueEstimator.Discord;

[JsonSerializable(typeof(DiscorbConfig))]
public partial class DiscorbConfigContext : JsonSerializerContext
{
    
}

public class DiscorbConfig
{
    public required string Token { get; init; }
    
    public string? Proxy { get; set; }

    public DiscorbConfig(string token, string? proxy)
    {
        Token = token;
        Proxy = proxy;
    }
}