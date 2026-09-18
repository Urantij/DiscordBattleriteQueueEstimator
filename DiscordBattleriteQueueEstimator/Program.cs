using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Discord;
using DiscordBattleriteQueueEstimator.Routes;
using DiscordBattleriteQueueEstimator.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DiscordBattleriteQueueEstimator;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

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
            DiscorbConfig? config =
                discordNode?.Deserialize(typeof(DiscorbConfig), DiscorbConfigContext.Default) as DiscorbConfig;
            if (config != null)
            {
                OptionsWrapper<DiscorbConfig> optionsWrapper = new(config);

                builder.Services.AddSingleton<IOptions<DiscorbConfig>>(_ => optionsWrapper);
            }
        }

        builder.Services.AddDbContextFactory<MyPoorLilContext>(optionsBuilder =>
        {
            optionsBuilder.UseSqlite($"Data Source=db.sqlite;");
            // этого друга нужно закомментить перед первой CompiledModels компиляции, удачи!
            optionsBuilder.UseModel(DiscordBattleriteQueueEstimator.CompiledModels.MyPoorLilContextModel.Instance);
        });

        builder.Services.AddSingleton<Database>();

        builder.Services.AddSingleton<Discorb>();
        builder.Services.AddHostedService<Discorb>(p => p.GetRequiredService<Discorb>());

        builder.Services.AddSingleton<DiscorbCommander>();
        builder.Services.AddHostedService<DiscorbCommander>(p => p.GetRequiredService<DiscorbCommander>());

        builder.Services.AddSingleton<Work.Worker>();
        builder.Services.AddHostedService<Work.Worker>(p => p.GetRequiredService<Work.Worker>());

        builder.Services.AddSingleton<Work.MatchObserver>();
        builder.Services.AddHostedService<Work.MatchObserver>(p => p.GetRequiredService<Work.MatchObserver>());

        // builder.Services.AddRouting(options =>
        //     options.ConstraintMap.Add("ulong", typeof(UlongRouteConstraint)));

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolver = MyJsonSirContext.Default;
        });

        WebApplication host = builder.Build();

        using (IServiceScope provider = host.Services.CreateScope())
        {
            ILogger logger = provider.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("info");
            logger.LogDebug("debug");

            // проверка, есть ли дискорд
            {
                IOptions<DiscorbConfig>? discord = provider.ServiceProvider.GetService<IOptions<DiscorbConfig>>();
                if (discord == null)
                    throw new Exception("Дискорд конфига не читается");
            }

            if (!File.Exists("./efbundle"))
            {
                throw new Exception("efbundle не найден");
            }

            using Process process = new();

            string? connectionString =
                provider.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("db");
            if (connectionString != null)
            {
                process.StartInfo.ArgumentList.Add("--connection");
                process.StartInfo.ArgumentList.Add(connectionString);
            }

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

        host.MapGet("/public/user/{id}/matches", MatchesRoutes.GetAsync);
        host.MapPost("/private/control/generatematches", ControlRoutes.GenerateMatchesAsync);
        host.MapGet("/private/control/health", ControlRoutes.GetHealthAsync);

        host.Run();
    }
}