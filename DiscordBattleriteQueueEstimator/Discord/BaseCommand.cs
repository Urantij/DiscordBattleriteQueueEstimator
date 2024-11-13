using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace DiscordBattleriteQueueEstimator.Discord;

public abstract class BaseCommand
{
    protected readonly ILogger _logger;

    protected BaseCommand(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());
    }

    public abstract Task DoAsync(GatewayClient sender, SlashCommandInteraction args);

    protected Task SendReplyAsync(SlashCommandInteraction args, string text)
    {
        return args.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties()
            .WithContent(text)
            .WithFlags(MessageFlags.Ephemeral)));
    }
}