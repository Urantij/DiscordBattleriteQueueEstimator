using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Discord;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiscordBattleriteQueueEstimator;

public class Program
{
    public static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(c => { c.TimestampFormat = "[HH:mm:ss] "; });

        // TODO В релизной сборке ему всё равно на этот сет минимум левел. Надо бы подумать над этим.
#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
        if (args.Contains("--debug"))
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        builder.Services.AddOptions<DiscorbConfig>()
            .Bind(builder.Configuration.GetSection("Discord"))
            // .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddDbContextFactory<MyContext>(optionsBuilder =>
        {
            optionsBuilder.UseSqlite($"Data Source=db.sqlite;");
        });

        builder.Services.AddSingleton<Database>();

        builder.Services.AddSingleton<Discorb>();
        builder.Services.AddHostedService<Discorb>(p => p.GetRequiredService<Discorb>());
        
        builder.Services.AddSingleton<DiscorbCommander>();
        builder.Services.AddHostedService<DiscorbCommander>(p => p.GetRequiredService<DiscorbCommander>());

        builder.Services.AddSingleton<Work.Worker>();
        builder.Services.AddHostedService<Work.Worker>(p => p.GetRequiredService<Work.Worker>());

        IHost host = builder.Build();

        using (IServiceScope provider = host.Services.CreateScope())
        {
            ILogger logger = provider.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("info");
            logger.LogDebug("debug");
            
            using var context = provider.ServiceProvider.GetRequiredService<MyContext>();
            context.Database.Migrate();
        }
        
        host.Run();
    }
}