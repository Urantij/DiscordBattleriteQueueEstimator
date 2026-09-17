exit 1

sudo docker run --rm -v "$(pwd):$(pwd)" -w "$(pwd)" mcr.microsoft.com/dotnet/sdk:10.0-noble-aot dotnet publish -c Release -r linux-x64 /p:EFOptimizeContext=false ./DiscordBattleriteQueueEstimator/DiscordBattleriteQueueEstimator.csproj

sudo chown -R $USER:$USER ./