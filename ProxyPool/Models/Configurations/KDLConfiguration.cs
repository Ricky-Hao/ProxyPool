using ProxyPool.Utils;

namespace ProxyPool.Models.Configurations
{
    public class KDLConfiguration
    {
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string ApiKey { get; set; } = null!;
        public int BatchSize { get; set; } = 10;
        public KuaiDaiLiType ProxyType
        {
            get
            {
                if (Type == null)
                    throw new ArgumentException($"Missing Type field in KuaiDaiLiConfiguration.");
                try
                {
                    return (KuaiDaiLiType)Enum.Parse(typeof(KuaiDaiLiType), Type, true);
                }
                catch
                {
                    throw new ArgumentException($"Could not convert Type {Type} to any KuaiDaiLiType.");
                }
            }
        }

        public void Validate()
        {
            ConfigurationTools.EnsureStringNotNullOrEmpty(Name);
            ConfigurationTools.EnsureStringNotNullOrEmpty(Type);
            ConfigurationTools.EnsureStringNotNullOrEmpty(OrderId);
            ConfigurationTools.EnsureStringNotNullOrEmpty(ApiKey);
        }
    }

    public enum KuaiDaiLiType
    {
        OpenProxy,
        TunnelProxy,
        PrivateProxy
    }
}
