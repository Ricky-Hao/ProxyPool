using ProxyPool.Models;

namespace ProxyPool.ProxySources.Interfaces
{
    public interface IProxySource : IDisposable
    {
        public string SourceName { get; }
        public DateTime LastFetchTime { get; set; }
        public Task<List<Proxy>> FetchProxy();
    }
}
