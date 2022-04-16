using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using ProxyPool.Models;
using ProxyPool.Models.Apis;
using ProxyPool.Models.Configurations;
using ProxyPool.Repositories;
using System.Runtime.CompilerServices;

namespace ProxyPool.Controllers
{
    [Area("Api")]
    public class ProxyController : Controller
    {
        private readonly ProxyRepository proxyRepo;
        private readonly ILogger logger;
        private readonly ProxyPoolConfiguration configuration;
        public ProxyController(ProxyRepository proxyRepo, ILogger<ProxyController> logger, ProxyPoolConfiguration configuration)
        {
            this.proxyRepo = proxyRepo;
            this.logger = logger;
            this.configuration = configuration;
        }


        public async Task<ActionResult<Proxy>> Get(bool? onlyHttps = null, int? latency = null, int? successCount = null, int? failCount = null)
        {
            var filter = proxyRepo.AvaliableFilterBuilder(onlyHttps, latency, successCount, failCount);
            var sampleStage = BsonDocument.Parse(@"{$sample: {size: 1}}");
            Proxy? proxy;

            proxy = await proxyRepo.Collection.Aggregate().Match(filter).AppendStage<Proxy>(sampleStage).FirstOrDefaultAsync();

            if (proxy == null)
                return NotFound();
            return Ok(proxy);
        }

        public async Task<ActionResult<ProxyStatusResponse>> Status(bool? onlyHttps = null, int? latency = null, int? successCount = null, int? failCount = null)
        {
            var response = new ProxyStatusResponse()
            {
                TotalCount = await proxyRepo.CountAvaliableAsync(onlyHttps: onlyHttps, latency: latency, successCount: successCount, failCount: failCount),
                HttpsCount = await proxyRepo.CountAvaliableAsync(onlyHttps: true, latency: latency, successCount: successCount, failCount: failCount),
                CheckingCount = await proxyRepo.CountCheckingAsync(),
                WaitToCheckCount = await proxyRepo.CountWaitToCheckAsync(),
                FoundCount = await proxyRepo.CountAsync()
            };
            return response;
        }

        [HttpDelete("[area]/[controller]/[action]/{id:required:length(24)}")]
        public async Task<ActionResult> Delete(string id)
        {
            if (!ObjectId.TryParse(id, out var proxyId))
                return NotFound();

            var proxy = await proxyRepo.Collection.Find(f => f.Id == proxyId).FirstOrDefaultAsync();
            if (proxy == null)
                return NotFound();

            if (proxy.CheckFailCount + 1 >= configuration.CheckFailedCountLimit)
            {
                var deleteResult = await proxyRepo.Collection.DeleteOneAsync(f => f.Id == proxyId);
                if (deleteResult.DeletedCount > 0)
                    return Ok();
                else
                    return NotFound();
            }

            var result = await proxyRepo.Collection.UpdateOneAsync(f => f.Id == proxyId,
                Builders<Proxy>.Update
                    .Inc(f => f.CheckFailCount, 1)
                    .Set(f => f.LastCheckTime, DateTime.UtcNow));
            if(result.ModifiedCount > 0)
                return Ok(result);
            else
                return NotFound();
        }
    }
}
