namespace ProxyPool.Utils
{
    public class ConfigurationTools
    {
        public static void EnsureStringNotNullOrEmpty(string Value)
        {
            if (string.IsNullOrEmpty(Value))
                throw new ArgumentNullException(nameof(Value));
        }
    }
}
