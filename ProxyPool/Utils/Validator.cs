using ProxyPool.Models;
using ProxyPool.Models.Configurations;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ProxyPool.Utils
{
    public class Validator
    {
        private readonly ILogger logger;
        private readonly ProxyPoolConfiguration configuration;
        public Validator(ProxyPoolConfiguration configuration, ILogger<Validator> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task<Proxy> ValidateAsync(Proxy proxy, int timeout, CancellationToken cancellationToken)
        {
            if (await ValidateByUrlAsync(proxy, configuration.HttpCheckUrl, timeout, cancellationToken, configuration.HttpCheckHeaders) is long httpLatency
                && httpLatency > 0)
            {
                proxy.Http.Status = true;
                proxy.Http.Latency = httpLatency;
            }
            else
            {
                proxy.Http.Status = false;
                proxy.Http.Latency = -1;
            }

            if (await ValidateByUrlAsync(proxy, configuration.HttpsCheckUrl, timeout, cancellationToken, configuration.HttpsCheckHeaders) is long httpsLatency
                && httpsLatency > 0)
            {
                proxy.Https.Status = true;
                proxy.Https.Latency = httpsLatency;
            }
            else
            {
                proxy.Https.Status = false;
                proxy.Https.Latency = -1;
            }

            proxy.LastCheckTime = DateTime.UtcNow;
            proxy.CheckSuccessCount++;
            if (proxy.Http.Status == false && proxy.Https.Status == false)
                proxy.CheckFailCount++;

            return proxy;
        }

        private async Task<long> ValidateByUrlAsync(Proxy proxy, string url, int timeout, CancellationToken cancellationToken, Dictionary<string, string>? headers = null)
        {
            var httpHandler = new HttpClientHandler()
            {
                Proxy = new WebProxy(new Uri(proxy.Url))
            };
            using var client = new HttpClient(httpHandler) { Timeout = new TimeSpan(0, 0, timeout) };

            if (headers != null && headers.Count > 0)
                foreach(var header in headers)
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);

            var watch = new Stopwatch();
            try
            {
                watch.Start();
                var response = await client.GetAsync(url, cancellationToken);
                watch.Stop();
                if (response == null)
                    return -1;
                else if (response.StatusCode != HttpStatusCode.OK)
                    return -1;
                else
                {
                    if (url.StartsWith("https://api.vc.bilibili.com/dynamic_svr") || url.StartsWith("http://api.vc.bilibili.com/dynamic_svr"))
                    {
                        try
                        {
                            var result = JsonSerializer.Deserialize<BiliApiResponse>(response.Content.ReadAsStream(cancellationToken));
                            if (result == null)
                                return -1;
                            else if (result.Code != 0)
                                return -1;
                        }
                        catch (JsonException ex)
                        {
                            logger.LogDebug("[{proxy}] Json parse failed: {ex}", proxy.Url, ex);
                            return -1;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError("[{proxy}] Json parse unknown error: {ex}", proxy.Url, ex);
                            return -1;
                        }
                    }
                    else if (url.StartsWith("https://api.bilibili.com/x/v2/reply/reply") || url.StartsWith("http://api.bilibili.com/x/v2/reply/reply"))
                    {
                        try
                        {
                            var result = JsonSerializer.Deserialize<BiliApiResponse>(ExtractJQJson(response.Content.ReadAsStream(cancellationToken)));
                            if (result == null)
                                return -1;
                            else if (result.Code != 0)
                                return -1;
                        }
                        catch (JsonException ex)
                        {
                            logger.LogDebug("[{proxy}] Json parse failed: {ex}", proxy.Url, ex);
                            return -1;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError("[{proxy}] Json parse unknown error: {ex}", proxy.Url, ex);
                            return -1;
                        }
                    }
                    return watch.ElapsedMilliseconds;
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogTrace("[{proxy}] HTTP Request error: {ex}.", proxy.Url, ex);
                return -1;
            }
            catch (SocketException ex)
            {
                logger.LogTrace("[{proxy}] Socket Request error: {ex}.", proxy.Url, ex);
                return -1;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                logger.LogTrace("[{proxy}] Request cancel because timeout.", proxy.Url);
                return -1;
            }
            catch (OperationCanceledException)
            {
                logger.LogError("[{proxy}] Request Operation Canceled.", proxy.Url);
                return -1;
            }
            catch (Exception ex)
            {
                logger.LogError("[{proxy}] Unknown error: {ex}.", proxy.Url, ex);
                return -1;
            }
            finally
            {
                if (watch.IsRunning)
                    watch.Stop();
            }
        }

        private string ExtractJQJson(Stream stream)
        {
            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var buf = new byte[memoryStream.Length];
            memoryStream.Read(buf, 0, buf.Length);
            return Encoding.UTF8.GetString(buf[40..^1]);
        }
    }
}
