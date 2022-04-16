using ProxyPool.Models;
using ProxyPool.Models.Configurations;
using ProxyPool.ProxySources.Interfaces;
using ProxyPool.Utils;
using System.Web;

namespace ProxyPool.ProxySources.KuaiDaiLi
{
    public class KDLOpenProxySource : IProxySource
    {
        private const string GetProxyUrl = "http://dev.kdlapi.com/api/getproxy";
        private const string DefaultQuery = "protocol=1&method=1&quality=0&sep=2";
        private readonly KDLConfiguration configuration;
        private readonly ILogger logger;
        private readonly string logPrefix;
        private readonly ProxyParser proxyParser;

        private DateTime lastFetchTime = DateTime.MinValue;

        public string SourceName { get { return configuration.Name; } }
        public DateTime LastFetchTime { get { return lastFetchTime; } set { lastFetchTime = value; } }
        public KDLOpenProxySource(KDLConfiguration configuration, ILogger<KDLOpenProxySource> logger, ProxyParser proxyParser)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.proxyParser = proxyParser;
            logPrefix = $"[{configuration.Name}]";
        }

        public void Dispose()
        {
        }

        public async Task<List<Proxy>> FetchProxy()
        {
            using var client = new HttpClient();
            var queryDict = HttpUtility.ParseQueryString(DefaultQuery);
            queryDict.Add("orderid", configuration.OrderId);
            queryDict.Add("num", configuration.BatchSize.ToString());

            var builder = new UriBuilder(GetProxyUrl)
            {
                Query = queryDict.ToString()
            };

            string? response = null;
            var proxies = new List<Proxy>();

            try
            {
                response = await client.GetStringAsync(builder.Uri.AbsoluteUri);
            }
            catch (Exception ex)
            {
                logger.LogError("{logPrefix} Unknown error: {ex}.", logPrefix, ex);
            }

            if (response == null)
            {
                logger.LogError("{logPrefix} Failed to fetch {url}.", logPrefix, builder.Uri.AbsoluteUri);
                return proxies;
            }

            if (response.StartsWith("ERROR") || response.StartsWith("{"))
            {
                logger.LogError("{logPrefix} Failed to fetch {url} with error: {response}.", logPrefix, builder.Uri.AbsoluteUri, response);
                return proxies;
            }

            proxies = proxyParser.ParseTxt(SourceName, response, "\n");
            return proxies.DistinctBy(x => x.Url).ToList();
        }
    }
}
