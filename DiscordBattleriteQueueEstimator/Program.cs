using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

        // Очень жаль, но нормального способа читать конфиги в aot тупо нет.
        {
            string settingsContent = File.ReadAllText("./appsettings.json");

            JsonNode? discordNode = JsonNode.Parse(settingsContent)?["Discord"];
            if (discordNode == null)
                throw new Exception("Дискорд конфига не вижу");

            DiscorbConfig? config =
                discordNode.Deserialize(typeof(DiscorbConfig), DiscorbConfigContext.Default) as DiscorbConfig;
            if (config == null)
                throw new Exception("Дискорд конфига не читается");

            OptionsWrapper<DiscorbConfig> optionsWrapper = new(config);

            builder.Services.AddSingleton<IOptions<DiscorbConfig>>(_ => optionsWrapper);
        }

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

            if (!File.Exists("./efbundle"))
            {
                throw new Exception("efbundle не найден");
            }

            using Process process = new();
            process.StartInfo.FileName = "./efbundle";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            process.WaitForExit();

            int code = process.ExitCode;

            if (code != 0)
                throw new Exception($"Код выхода миграции {code}");
        }

        host.Run();
    }
}