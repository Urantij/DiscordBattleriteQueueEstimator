# optionsBuilder.UseModel(DiscordBattleriteQueueEstimator.CompiledModels.MyPoorLilContextModel.Instance);
# не забудь закоментить перед генерацией

dotnet ef dbcontext optimize --precompile-queries --nativeaot --output-dir CompiledModels --namespace DiscordBattleriteQueueEstimator.CompiledModels

# :)
sed -i '/using DiscordBattleriteQueueEstimator.Shared.Data;/c\using DiscordBattleriteQueueEstimator.Shared.Data;\nusing DiscordBattleriteQueueEstimator.Shared.Data.Models;' ./CompiledModels/HeroCommand.EFInterceptors.MyPoorLilContext.cs
sed -i '/using DiscordBattleriteQueueEstimator.Shared.Data;/c\using DiscordBattleriteQueueEstimator.Shared.Data;\nusing DiscordBattleriteQueueEstimator.Shared.Data.Models;' ./CompiledModels/TimeCommand.EFInterceptors.MyPoorLilContext.cs

# optionsBuilder.UseModel(DiscordBattleriteQueueEstimator.CompiledModels.MyPoorLilContextModel.Instance);
# не забудь откомментить перед паблишем 