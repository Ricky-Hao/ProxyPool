using ProxyPool.Utils;
using System.Diagnostics.Contracts;

namespace ProxyPool.Models.Configurations
{
    public class GeneralProxyConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public List<Dictionary<string, string>> Payload { get; set; } = new();

        public void Validate()
        {
            ConfigurationTools.EnsureStringNotNullOrEmpty(Url);
            ConfigurationTools.EnsureStringNotNullOrEmpty(Name);
        }
    }
}
