using ProxyPool.ProxySources.Interfaces;

namespace ProxyPool.Utils
{
    public class GlobalStatus
    {
        public readonly List<IProxySource> ProxySources = new();
        public readonly SemaphoreSlim SourceLock = new(1);
    }
}
