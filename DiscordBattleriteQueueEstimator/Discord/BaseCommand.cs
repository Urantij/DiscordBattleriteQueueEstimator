using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace DiscordBattleriteQueueEstimator.Discord;

public abstract class BaseCommand
{
    protected readonly ILogger _logger;

    protected BaseCommand(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());
    }

    public abstract Task DoAsync(DiscordClient sender, InteractionCreateEventArgs args);

    protected Task SendReplyAsync(InteractionCreateEventArgs args, string text)
    {
        return args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder()
                .WithContent(text)
                .AsEphemeral());
    }
}