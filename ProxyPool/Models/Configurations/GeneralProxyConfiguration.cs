using ProxyPool.Utils;

namespace ProxyPool.Models.Configurations
{
    public class GeneralProxyConfiguration
    {
        public string Name { get; set; } = null!;
        public string Url { get; set; } = null!;

        public void Validate()
        {
            ConfigurationTools.EnsureStringNotNullOrEmpty(Url);
            ConfigurationTools.EnsureStringNotNullOrEmpty(Name);
        }
    }
}
