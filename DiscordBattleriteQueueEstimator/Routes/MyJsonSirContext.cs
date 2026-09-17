using System.Text.Json.Serialization;

namespace DiscordBattleriteQueueEstimator.Routes;

[JsonSerializable(typeof(WebMatchModel[]))]
[JsonSerializable(typeof(HealthWebResult))]
public partial class MyJsonSirContext : JsonSerializerContext
{
}