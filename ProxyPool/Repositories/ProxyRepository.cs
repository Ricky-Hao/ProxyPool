using Microsoft.AspNetCore.Razor.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using ProxyPool.Models;
using ProxyPool.Models.Configurations;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace ProxyPool.Repositories
{
    public class ProxyRepository
    {
        public readonly IMongoCollection<Proxy> Collection;

        private readonly ProxyPoolConfiguration configuration;
        private readonly ILogger logger;
        private Expression<Func<Proxy, bool>> WaitToCheckFilter
        {
            get
            {
                var lastCheckTime = DateTimeOffset.UtcNow.AddSeconds(-configuration.CheckIntervalSeconds).UtcDateTime;
                return f => f.LastCheckTime < lastCheckTime && f.CheckFailCount < configuration.CheckFailedCountLimit && f.Checking == false;
            }
        }
        public ProxyRepository(ILogger<ProxyRepository> logger, IMongoDatabase database, ProxyPoolConfiguration configuration)
        {
            this.configuration = configuration;
            this.logger = logger;

            var indexes = new List<CreateIndexModel<Proxy>>()
            {
                new CreateIndexModel<Proxy>(Builders<Proxy>.IndexKeys.Ascending(f => f.Source), new CreateIndexOptions(){Background=true}),
                new CreateIndexModel<Proxy>(Builders<Proxy>.IndexKeys.Ascending(f => f.AddTime), new CreateIndexOptions(){Background=true}),
                new CreateIndexModel<Proxy>(Builders<Proxy>.IndexKeys.Ascending(f => f.Http.Status), new CreateIndexOptions(){Background=true}),
                new CreateIndexModel<Proxy>(Builders<Proxy>.IndexKeys.Ascending(f => f.Https.Status), new CreateIndexOptions(){Background=true}),
                new CreateIndexModel<Proxy>(Builders<Proxy>.IndexKeys.Ascending(f => f.CheckSuccessCount).Ascending(f => f.CheckFailCount), new CreateIndexOptions(){Background=true}),
                new CreateIndexModel<Proxy>(Builders<Proxy>.IndexKeys.Ascending(f => f.Type).Ascending(f => f.Host).Ascending(f => f.Port), new CreateIndexOptions(){Background=true, Unique=true}),
            };

            if (!database.ListCollectionNames().ToList().Contains("proxies"))
            {
                database.CreateCollection("proxies");
            }
            Collection = database.GetCollection<Proxy>("proxies");
            Collection.Indexes.CreateMany(indexes);

        }

        public async Task<bool> AddProxy(Proxy proxy, CancellationToken cancellationToken = default)
        {
            var cursor = await Collection.FindAsync(f => f.Host == proxy.Host && f.Port == proxy.Port && f.Type == proxy.Type, cancellationToken: cancellationToken);
            await cursor.MoveNextAsync(cancellationToken);

            if (cursor.Current.FirstOrDefault() != null)
                return false;

            await Collection.InsertOneAsync(proxy, cancellationToken: cancellationToken);
            return true;
        }

        public async Task<bool> DeleteProxyById(string id, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out ObjectId oid))
                return false;

            var result = await Collection.DeleteOneAsync(f => f.Id == oid, cancellationToken: cancellationToken);
            return result.DeletedCount > 0;
        }

        public async Task<bool> DeleteProxy(Proxy proxy, CancellationToken cancellationToken = default)
        {
            if (proxy.Id == default)
                return false;

            var result = await Collection.DeleteOneAsync(f => f.Id == proxy.Id, cancellationToken: cancellationToken);
            return result.DeletedCount > 0;
        }

        public async Task<UpdateResult> IncAsync(Proxy proxy, Expression<Func<Proxy, int>> field, int amount = 1, CancellationToken cancellationToken = default)
        {
            return await Collection.UpdateOneAsync(f => f.Id == proxy.Id, Builders<Proxy>.Update.Inc(field, amount), cancellationToken: cancellationToken);
        }

        public async Task<UpdateResult> SetAsync<T>(Proxy proxy, Expression<Func<Proxy, T>> field, T value, CancellationToken cancellationToken = default)
        {
            return await Collection.UpdateOneAsync(f => f.Id == proxy.Id, Builders<Proxy>.Update.Set(field, value), cancellationToken: cancellationToken);
        }

        public FilterDefinition<Proxy> AvaliableFilterBuilder(bool? onlyHttps = null, int? latency = null, int? successCount = null, int? failCount = null)
        {
            var builder = Builders<Proxy>.Filter;
            if (!successCount.HasValue)
                successCount = 1;
            if (!failCount.HasValue)
                failCount = configuration.CheckFailedCountLimit;
            var filter = builder.Lte(f => f.CheckFailCount, failCount) & builder.Gt(f => f.CheckSuccessCount, successCount.Value);

            if (latency.HasValue)
            {
                if (onlyHttps.HasValue && onlyHttps.Value)
                    filter &= builder.Eq(f => f.Https.Status, true) & builder.Lte(f => f.Https.Latency, latency.Value);
                else
                    filter &= (builder.Eq(f => f.Http.Status, true) & builder.Lte(f => f.Http.Latency, latency.Value) | builder.Eq(f => f.Https.Status, true) & builder.Lte(f => f.Https.Latency, latency.Value));
            }
            return filter;
        }

        public async Task<long> CountAvaliableAsync(bool? onlyHttps = null, int? latency = null, int? successCount = null, int? failCount = null, CancellationToken cancellationToken = default)
        {
            try
            {
                return await Collection.CountDocumentsAsync(AvaliableFilterBuilder(onlyHttps, latency, successCount, failCount), cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    logger.LogDebug("Query cancelled.");
                else
                    logger.LogError("Query unknown cancelled: {ex}", ex);
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError("Query unknown exception: {ex}", ex);
                return 0;
            }
        }

        public async Task<long> CountWaitToCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await Collection.CountDocumentsAsync(WaitToCheckFilter, cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    logger.LogDebug("Query cancelled.");
                else
                    logger.LogError("Query unknown cancelled: {ex}", ex);
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError("Query unknown exception: {ex}", ex);
                return 0;
            }
        }

        public async Task<long> CountCheckingAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await Collection.CountDocumentsAsync(f => f.Checking == true, cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    logger.LogDebug("Query cancelled.");
                else
                    logger.LogError("Query unknown cancelled: {ex}", ex);
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError("Query unknown exception: {ex}", ex);
                return 0;
            }
        }

        public async Task<long> CountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await Collection.CountDocumentsAsync(new BsonDocument(), cancellationToken: cancellationToken);
            }
            catch (TaskCanceledException ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    logger.LogDebug("Query cancelled.");
                else
                    logger.LogError("Query unknown cancelled: {ex}", ex);
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError("Query unknown exception: {ex}", ex);
                return 0;
            }
        }

        public async IAsyncEnumerable<Proxy> FindWaitToCheckAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var cursor = await Collection.FindAsync(WaitToCheckFilter, cancellationToken: cancellationToken);
            while (await cursor.MoveNextAsync(cancellationToken))
            {
                foreach (var doc in cursor.Current)
                    yield return doc;
            }
        }
    }
}
