using ProxyPool.Models;
using System.Text.RegularExpressions;

namespace ProxyPool.Utils
{
    public class ProxyParser
    {
        public readonly Regex IPPattern = new(@"([0,1]?\d{1,2}|2([0-4][0-9]|5[0-5]))(\.([0,1]?\d{1,2}|2([0-4][0-9]|5[0-5]))){3}\:\d{2,5}", RegexOptions.Compiled);
        private readonly ILogger logger;

        public ProxyParser(ILogger<ProxyParser> logger) => this.logger = logger;
        public List<Proxy> ParseTxt(string sourceName, string data, string seperator)
        {
            var addTime = DateTime.UtcNow;
            var proxies = new List<Proxy>();
            foreach (var line in data.Split(seperator))
            {
                var url = line.Trim();
                try
                {
                    if (IPPattern.IsMatch(url))
                    {
                        var urlSplit = url.Split(':');
                        proxies.Add(new Proxy()
                        {
                            Host = urlSplit[0],
                            Port = int.Parse(urlSplit[1]),
                            AddTime = addTime,
                            Source = sourceName,
                            Type = ProxyTypeEnum.Http,
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Failed to parse {url}: {ex}.", url, ex);
                }
            }
            return proxies;
        }
    }
}
