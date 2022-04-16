using ProxyPool.Models.Configurations;
using ProxyPool.ProxySources;
using ProxyPool.ProxySources.KuaiDaiLi;
using ProxyPool.Repositories;
using ProxyPool.Utils;

namespace ProxyPool.Workers
{
    public class FetchProxyWorker : BackgroundService
    {
        private readonly ProxyPoolConfiguration configuration;
        private readonly ILogger logger;
        private readonly GlobalStatus status;
        private readonly ProxyRepository proxyRepo;

        public FetchProxyWorker(ProxyPoolConfiguration configuration, ILogger<FetchProxyWorker> logger, IHost host, ProxyParser proxyParser, ProxyRepository proxyRepository)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.proxyRepo = proxyRepository;

            status = host.Services.GetRequiredService<GlobalStatus>();

            status.SourceLock.Wait();
            try
            {
                foreach (var config in configuration.KuaiDaiLis)
                {
                    switch (config.ProxyType)
                    {
                        case KuaiDaiLiType.OpenProxy:
                            status.ProxySources.Add(new KDLOpenProxySource(config, host.Services.GetRequiredService<ILogger<KDLOpenProxySource>>(), proxyParser));
                            logger.LogInformation("Add proxy source: {name}.", config.Name);
                            break;
                        default:
                            throw new Exception($"Unsupport Proxy Type {config.ProxyType}.");
                    }
                }

                foreach(var config in configuration.GeneralProxies)
                {
                    status.ProxySources.Add(new GeneralProxySource(host.Services.GetRequiredService<ILogger<GeneralProxySource>>(),
                        host.Services.GetRequiredService<ProxyParser>(), config));
                }

            }
            finally
            {
                status.SourceLock.Release();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var task = new Task(async () => await FetchProxyAsync(stoppingToken));
            task.Start();
            await task.WaitAsync(stoppingToken);
        }

        private async Task FetchProxyAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (await proxyRepo.CountAvaliableAsync(cancellationToken: stoppingToken) < configuration.FetchTriggerProxyCount)
                {
                    logger.LogInformation("Fetch new proxy.");
                    foreach (var source in status.ProxySources)
                    {
                        var newProxies = await source.FetchProxy();
                        foreach (var proxy in newProxies)
                        {
                            if (await proxyRepo.AddProxy(proxy, cancellationToken: stoppingToken))
                            {
                                logger.LogDebug("[{source}] Found proxy: {url}.", source.SourceName, proxy.Url);
                            }
                        }
                        logger.LogInformation("[{source}] Found {count} proxies.", source.SourceName, newProxies.Count);
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(configuration.FetchIntervalSeconds), stoppingToken);
                logger.LogInformation("Avaliable Total {total}, Https {https}, Checking {check}, Wait to Check {wait}, Founded {found}.",
                    await proxyRepo.CountAvaliableAsync(cancellationToken: stoppingToken),
                    await proxyRepo.CountAvaliableAsync(onlyHttps: true, cancellationToken: stoppingToken),
                    await proxyRepo.CountCheckingAsync(cancellationToken: stoppingToken),
                    await proxyRepo.CountWaitToCheckAsync(cancellationToken: stoppingToken),
                    await proxyRepo.CountAsync(cancellationToken: stoppingToken));
            };
        }
    }
}
