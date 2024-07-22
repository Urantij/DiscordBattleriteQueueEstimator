using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Discord.Commands;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

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
        DiscordClient client = _discorb.GetClient();

        client.InteractionCreated += OnComponentInteractionCreated;

        IReadOnlyList<DiscordApplicationCommand>? discordApplicationCommands =
            await client.GetGlobalApplicationCommandsAsync();

        foreach (CommandInfo commandInfo in _commands)
        {
            DiscordApplicationCommand? applicationCommand =
                discordApplicationCommands.FirstOrDefault(c => c.Name == commandInfo.Name);

            // ОНИ РЕАЛЬНО СДЕЛАЛИ ТАК ЧТО == НУЛЛ КИДАЕТ НУЛЛ РЕФЕРЕНС. БЕЛИССИМО
            if (applicationCommand is null)
            {
                _logger.LogInformation("Создаём команду.");

                applicationCommand = await client.CreateGlobalApplicationCommandAsync(
                    new DiscordApplicationCommand(
                        commandInfo.Name, commandInfo.Description, allowDMUsage: true));
            }

            if (applicationCommand.AllowDMUsage != true)
            {
                _logger.LogDebug("Команда без дма, меняем, айди {id}", commandInfo.Id);
                applicationCommand =
                    await client.EditGlobalApplicationCommandAsync(applicationCommand.Id,
                        (a) => { a.AllowDMUsage = true; });
            }

            commandInfo.Id = applicationCommand.Id;

            _logger.LogDebug("Команда {name} найдена, айди {id}", commandInfo.Name, commandInfo.Id);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _discorb.GetClient().InteractionCreated -= OnComponentInteractionCreated;

        return Task.CompletedTask;
    }

    private Task OnComponentInteractionCreated(DiscordClient sender, InteractionCreateEventArgs args)
    {
        _logger.LogDebug("Получена интеракция {id}", args.Interaction.Data.Id);

        BaseCommand? command = null;
        if (args.Interaction.Data.Id == _infoCommand.Id)
            command = new InfoCommand(_worker, _loggerFactory);
        else if (args.Interaction.Data.Id == _timeCommand.Id)
            command = new TimeCommand(_provider.GetRequiredService<Database>(), _loggerFactory);
        else if (args.Interaction.Data.Id == _heroCommand.Id)
            command = new HeroCommand(_provider.GetRequiredService<Database>(), _loggerFactory);

        if (command != null)
            try
            {
                return command.DoAsync(sender, args);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Команда {name} провалилась", command.GetType().Name);
            }

        return Task.CompletedTask;
    }
}