using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Discord.Commands;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace DiscordBattleriteQueueEstimator.Discord;

public class DiscorbCommander : IHostedService
{
    private readonly CommandInfo _infoCommand = new()
    {
        Name = "info",
        Description = "queue info"
    };

    private readonly CommandInfo _timeCommand = new()
    {
        Name = "time",
        Description = "sadge statistic"
    };

    private readonly CommandInfo _heroCommand = new()
    {
        Name = "hero",
        Description = "sadge statistic"
    };

    private readonly CommandInfo[] _commands;

    private readonly Discorb _discorb;
    private readonly Work.Worker _worker;
    private readonly IServiceProvider _provider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DiscorbCommander> _logger;

    public DiscorbCommander(Discorb discorb, Work.Worker worker, IServiceProvider provider,
        ILoggerFactory loggerFactory)
    {
        _discorb = discorb;
        _worker = worker;
        _provider = provider;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DiscorbCommander>();

        _commands = [_infoCommand, _timeCommand, _heroCommand];
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        GatewayClient client = _discorb.GetClient();

        if (_discorb.Application == null)
        {
            _logger.LogError("Аппликейшн недоступен.");
            return;
        }

        client.InteractionCreate += ClientOnInteractionCreate;

        IReadOnlyList<ApplicationCommand> discordApplicationCommands =
            await client.Rest.GetGlobalApplicationCommandsAsync(_discorb.Application.Id,
                cancellationToken: cancellationToken);

        foreach (CommandInfo commandInfo in _commands)
        {
            ApplicationCommand? applicationCommand =
                discordApplicationCommands.FirstOrDefault(c => c.Name == commandInfo.Name);

            // Комментарий ниже относится к библиотеке dsharpplus
            // Но это такая тупая хуйня, что я решил его оставить
            // ОНИ РЕАЛЬНО СДЕЛАЛИ ТАК ЧТО == НУЛЛ КИДАЕТ НУЛЛ РЕФЕРЕНС. БЕЛИССИМО
            if (applicationCommand is null)
            {
                _logger.LogInformation("Создаём команду.");

                applicationCommand = await client.Rest.CreateGlobalApplicationCommandAsync(_discorb.Application.Id,
                    new SlashCommandProperties(commandInfo.Name, commandInfo.Description).WithContexts([
                        InteractionContextType.BotDMChannel
                    ]));
            }

            if (applicationCommand.Contexts?.Any(c => c == InteractionContextType.BotDMChannel) != true)
            {
                _logger.LogDebug("Команда без дма, меняем, айди {id}", applicationCommand.Id);
                applicationCommand = await client.Rest.ModifyGlobalApplicationCommandAsync(
                    applicationCommand.ApplicationId, applicationCommand.Id,
                    action => action.AddContexts([InteractionContextType.BotDMChannel]), cancellationToken: cancellationToken);
            }

            commandInfo.Id = applicationCommand.Id;

            _logger.LogDebug("Команда {name} найдена, айди {id}", commandInfo.Name, commandInfo.Id);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _discorb.GetClient().InteractionCreate -= ClientOnInteractionCreate;

        return Task.CompletedTask;
    }

    private async ValueTask ClientOnInteractionCreate(Interaction arg)
    {
        if (arg is not SlashCommandInteraction slashCommandInteraction)
        {
            _logger.LogWarning("Странная интеракция {name}", arg.GetType().Name);
            return;
        }

        _logger.LogDebug("Получена интеракция {id}", slashCommandInteraction.Data.Id);

        BaseCommand? command = null;
        if (slashCommandInteraction.Data.Id == _infoCommand.Id)
            command = new InfoCommand(_worker, _loggerFactory);
        else if (slashCommandInteraction.Data.Id == _timeCommand.Id)
            command = new TimeCommand(_provider.GetRequiredService<Database>(), _loggerFactory);
        else if (slashCommandInteraction.Data.Id == _heroCommand.Id)
            command = new HeroCommand(_provider.GetRequiredService<Database>(), _loggerFactory);

        if (command != null)
            try
            {
                await command.DoAsync(_discorb.GetClient(), slashCommandInteraction);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Команда {name} провалилась", command.GetType().Name);
            }
    }

    public static string GetDefaultHelpText()
    {
        return
            """
            Can't get information about you.
            This bot allows you to find out your time on certain heroes, as well as time spent in game modes.
            The bot uses Rich Presence in discord to get information about you.
            To access these stats, make sure you have enabled your activity display in Discord.
            """;
    }
}