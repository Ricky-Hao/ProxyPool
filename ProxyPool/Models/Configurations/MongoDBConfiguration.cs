using ProxyPool.Utils;

namespace ProxyPool.Models.Configurations
{
    public class MongoDBConfiguration
    {
        public string Url { get; set; } = null!;
        public string Database { get; set; } = null!;

        public void Validate()
        {
            ConfigurationTools.EnsureStringNotNullOrEmpty(Url);
            ConfigurationTools.EnsureStringNotNullOrEmpty(Database);
        }
    }
}
