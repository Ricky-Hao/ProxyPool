using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;
using ProxyPool.Models;
using ProxyPool.Models.Configurations;
using ProxyPool.Repositories;

namespace ProxyPool.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        public readonly ProxyPoolConfiguration Configuration;
        public readonly ProxyRepository ProxyRepo;
        public List<Proxy> Proxies = new();

        public IndexModel(ILogger<IndexModel> logger, ProxyRepository proxyRepo, ProxyPoolConfiguration configuration)
        {
            _logger = logger;
            ProxyRepo = proxyRepo;
            Configuration = configuration;
        }

        public void OnGet()
        {
            Proxies = ProxyRepo.Collection.Find(f => f.CheckSuccessCount > 0).SortByDescending(f => f.AddTime).ToList();
        }
    }
}