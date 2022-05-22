using MongoDB.Driver;
using ProxyPool.Models;
using ProxyPool.Models.Configurations;
using ProxyPool.Repositories;
using ProxyPool.Utils;
using System.Threading.Tasks.Dataflow;

namespace ProxyPool.Workers
{
    public class ValidateWorker : BackgroundService
    {
        private readonly ILogger logger;
        private readonly ProxyPoolConfiguration configuration;
        private readonly ProxyRepository proxyRepo;
        private readonly ActionBlock<Proxy> actionBlock;
        private readonly CancellationTokenSource tokenSource = new();

        public ValidateWorker(ProxyPoolConfiguration configuration, ILogger<ValidateWorker> logger, ProxyRepository proxyRepo, Validator validator)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.proxyRepo = proxyRepo;

            actionBlock = new ActionBlock<Proxy>(async proxy =>
            {
                logger.LogDebug("Check proxy: {url}.", proxy.Url);
                if (await validator.ValidateAsync(proxy, configuration.CheckTimeout, tokenSource.Token) is Proxy newProxy)
                {
                    if (newProxy.Http.Status == false && newProxy.Https.Status == false)
                    {
                        // Validate failed
                        if (newProxy.CheckFailCount < configuration.CheckFailedCountLimit)
                            await proxyRepo.Collection.UpdateOneAsync(f => f.Id == newProxy.Id,
                            Builders<Proxy>.Update
                                .Inc(f => f.CheckFailCount, 1)
                                .Set(f => f.Checking, false)
                                .Set(f => f.LastCheckTime, DateTime.UtcNow),
                            cancellationToken: tokenSource.Token);
                        else
                            await proxyRepo.Collection.UpdateOneAsync(f => f.Id == newProxy.Id,
                                Builders<Proxy>.Update
                                    .Inc(f => f.CheckFailCount, 1)
                                    .Set(f => f.CheckSuccessCount, 0)
                                    .Set(f => f.Http, newProxy.Http)
                                    .Set(f => f.Https, newProxy.Https)
                                    .Set(f => f.Checking, false)
                                    .Set(f => f.LastCheckTime, DateTime.UtcNow),
                                cancellationToken: tokenSource.Token);
                    }
                    else
                    {
                        // Validate success
                        await proxyRepo.Collection.UpdateOneAsync(f => f.Id == newProxy.Id,
                            Builders<Proxy>.Update
                                .Set(f => f.Http, newProxy.Http)
                                .Set(f => f.Https, newProxy.Https)
                                .Set(f => f.CheckFailCount, 0)
                                .Inc(f => f.CheckSuccessCount, 1)
                                .Set(f => f.Checking, false)
                                .Set(f => f.LastCheckTime, DateTime.UtcNow),
                            cancellationToken: tokenSource.Token);
                        logger.LogInformation("[{source}] Validate proxy {url} with http {http} and https {https}.", newProxy.Source, newProxy.Url, newProxy.Http.Latency, newProxy.Https.Latency);
                    }
                }
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = configuration.CheckConcurrency });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var task = new Task(async () => await ValidateAsync(stoppingToken));
            task.Start();
            await task.WaitAsync(stoppingToken);
        }

        private async Task ValidateAsync(CancellationToken stoppingToken)
        {
            var resetResult = await proxyRepo.Collection.UpdateManyAsync(f => f.Checking == true, Builders<Proxy>.Update.Set(f => f.Checking, false), cancellationToken: stoppingToken);
            logger.LogInformation("Reset {count} proxy checking stat.", resetResult.ModifiedCount);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (actionBlock.InputCount < configuration.CheckConcurrency)
                {
                    try
                    {
                        var cursor = proxyRepo.FindWaitToCheckAsync(stoppingToken);

                        await foreach (var proxy in cursor)
                        {
                            if (actionBlock.InputCount < configuration.CheckConcurrency)
                            {
                                var postResult = actionBlock.Post(proxy);
                                if (postResult)
                                    await proxyRepo.SetAsync(proxy, f => f.Checking, true, stoppingToken);
                            }
                            else
                                break;
                        }
                    }
                    catch (TaskCanceledException ex)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            logger.LogInformation("ValidateAsync cancelled.");
                        else
                            logger.LogError("ValidateAsync unknown cancelled: {ex}", ex);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("{ex}", ex);
                    }
                }
                else
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            tokenSource.Cancel();
        }
    }
}
