﻿using MongoDB.Driver;
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
        private readonly Validator validator;

        public ValidateWorker(ProxyPoolConfiguration configuration, ILogger<ValidateWorker> logger, ProxyRepository proxyRepo, Validator validator)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.proxyRepo = proxyRepo;
            this.validator = validator;

            actionBlock = new ActionBlock<Proxy>(async proxy =>
            {
                try
                {
                    await ValidateImplAsync(proxy);
                }
                catch (Exception ex)
                {
                    logger.LogError("{ex}", ex);
                }
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = configuration.CheckConcurrency, BoundedCapacity = configuration.CheckConcurrency });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var task = Task.Run(async () => await ValidateAsync(stoppingToken), stoppingToken);
            await task.WaitAsync(stoppingToken);

            tokenSource.Cancel();
            actionBlock.Complete();
            await actionBlock.Completion;
        }

        private async Task ValidateAsync(CancellationToken stoppingToken)
        {
            var resetResult = await proxyRepo.Collection.UpdateManyAsync(f => f.Checking == true, Builders<Proxy>.Update.Set(f => f.Checking, false), cancellationToken: stoppingToken);
            logger.LogInformation("Reset {count} proxy checking stat.", resetResult.ModifiedCount);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cursor = proxyRepo.FindWaitToCheckAsync(stoppingToken);

                    await foreach (var proxy in cursor)
                    {
                         await actionBlock.SendAsync(proxy);
                    }

                    while (actionBlock.InputCount != 0)
                    {
                        await Task.Delay(configuration.CheckIntervalSeconds, stoppingToken);
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
        }

        private async Task ValidateImplAsync(Proxy proxy)
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
                        await proxyRepo.Collection.DeleteOneAsync(f => f.Id == newProxy.Id);
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
        }
    }
}
