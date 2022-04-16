using ProxyPool.Models;
using ProxyPool.Models.Configurations;
using ProxyPool.ProxySources.Interfaces;
using ProxyPool.Utils;
using System.Text;

namespace ProxyPool.ProxySources
{
    public class GeneralProxySource : IProxySource
    {
        private DateTime lastFetchTime = DateTime.MinValue;
        private readonly ILogger logger;
        private readonly ProxyParser proxyParser;
        private readonly GeneralProxyConfiguration configuration;

        public GeneralProxySource(ILogger<GeneralProxySource> logger, ProxyParser proxyParser, GeneralProxyConfiguration configuration)
        {
            this.logger = logger;
            this.proxyParser = proxyParser;
            this.configuration = configuration;
        }
        public string SourceName { get { return configuration.Name; } }

        public DateTime LastFetchTime { get => lastFetchTime; set => lastFetchTime = value; }

        public void Dispose()
        {
        }
        public async Task<List<Proxy>> FetchProxy()
        {
            await Task.Yield();
            var proxies = new List<Proxy>();
            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync(configuration.Url);
                if (response == null || !response.IsSuccessStatusCode)
                    return proxies;

                var charset = response.Content.Headers.ContentType?.CharSet;
                string data;
                if (charset != null)
                {
                    data = await new StreamReader(await response.Content.ReadAsStreamAsync(), encoding: Encoding.GetEncoding(charset)).ReadToEndAsync();
                }
                else
                    data = await response.Content.ReadAsStringAsync();

                proxies.AddRange(proxyParser.ParseTxt(SourceName, data, "\n"));
            }
            catch (Exception ex)
            {
                logger.LogError("[{name}] Failed to get {url}: {ex}.", SourceName, configuration.Url, ex);
            }
            return proxies.DistinctBy(x => x.Url).ToList();
        }
    }
}
