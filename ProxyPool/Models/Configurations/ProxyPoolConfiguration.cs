using ProxyPool.Utils;

namespace ProxyPool.Models.Configurations
{
    public class ProxyPoolConfiguration
    {
        public string HttpCheckUrl { get; set; } = null!;
        public Dictionary<string, string> HttpCheckHeaders { get; set; } = new();
        public string HttpsCheckUrl { get; set; } = null!;
        public Dictionary<string, string> HttpsCheckHeaders { get; set; } = new();
        public int CheckIntervalSeconds { get; set; } = 5;
        public int CheckFailedCountLimit { get; set; } = 3;
        public int CheckTimeout { get; set; } = 15;
        public int CheckConcurrency { get; set; } = 30;
        public int FetchIntervalSeconds { get; set; } = 5;
        public int FetchTriggerProxyCount { get; set; } = 100;
        public int ProxyTTLMinutes { get; set; } = 30;

        public MongoDBConfiguration MongoDB { get; set; } = null!;

        public List<KDLConfiguration> KuaiDaiLis { get; set; } = new();

        public List<GeneralProxyConfiguration> GeneralProxies { get; set; } = new();

        public void Validate()
        {
            ConfigurationTools.EnsureStringNotNullOrEmpty(HttpCheckUrl);
            ConfigurationTools.EnsureStringNotNullOrEmpty(HttpsCheckUrl);

            foreach (var config in KuaiDaiLis)
                config.Validate();

            foreach(var config in GeneralProxies)
                config.Validate();

            MongoDB.Validate();
        }
    }
}
