using ProxyPool.Utils;

namespace ProxyPool.Models.Configurations
{
    public class MongoDBConfiguration
    {
        public string Url { get; set; } = null!;
        public string Database { get; set; } = null!;

        public int MaxConnecting { get; set; } = 100;

        public int MaxConnectionPoolSize { get; set; } = 100;

        public void Validate()
        {
            ConfigurationTools.EnsureStringNotNullOrEmpty(Url);
            ConfigurationTools.EnsureStringNotNullOrEmpty(Database);
        }
    }
}
