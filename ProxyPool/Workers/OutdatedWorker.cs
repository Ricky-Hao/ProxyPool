using MongoDB.Driver;
using ProxyPool.Models.Configurations;
using ProxyPool.Repositories;

namespace ProxyPool.Workers
{
    public class OutdatedWorker : BackgroundService
    {
        private readonly ILogger logger;
        private readonly ProxyPoolConfiguration configuration;
        private readonly ProxyRepository proxyRepo;
        public OutdatedWorker(ILogger<OutdatedWorker> logger, ProxyRepository proxyRepo, ProxyPoolConfiguration configuration)
        {
            this.logger = logger;
            this.proxyRepo = proxyRepo;
            this.configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var task = new Task(async () => await FoundTTLAsync(stoppingToken));
            task.Start();
            await task.WaitAsync(stoppingToken);
        }
        private async Task FoundTTLAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    logger.LogInformation("Check Founded Pool's ttl.");

                    var outdatedTime = DateTimeOffset.UtcNow.AddMinutes(-configuration.ProxyTTLMinutes).UtcDateTime;
                    var deleteResult = await proxyRepo.Collection.DeleteManyAsync(f => f.AddTime < outdatedTime && f.CheckFailCount >= configuration.CheckFailedCountLimit, cancellationToken: stoppingToken);
                    logger.LogDebug("Delete {count} proxies because outdated.", deleteResult.DeletedCount);

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (TaskCanceledException ex)
                {
                    if (stoppingToken.IsCancellationRequested)
                        logger.LogInformation("FoundTTLAsync cancelled.");
                    else
                        logger.LogError("FoundTTLAsync unknown cancelled: {ex}", ex);
                }
                catch (Exception ex)
                {
                    logger.LogError("{ex}", ex);
                }
            }
        }
    }
}
