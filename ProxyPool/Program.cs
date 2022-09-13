using Microsoft.AspNetCore.Builder;
using MongoDB.Driver;
using ProxyPool.Models.Configurations;
using ProxyPool.ProxySources;
using ProxyPool.Repositories;
using ProxyPool.Utils;
using ProxyPool.Workers;
using Swashbuckle.AspNetCore.SwaggerGen.ConventionalRouting;
using System.Net;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// For Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGenWithConventionalRoutes();


/*
builder.Services.AddLogging(builder =>
{
    builder.AddGelf();
});
*/


builder.Services.AddSingleton<ProxyPoolConfiguration>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>().GetRequiredSection("ProxyPool").Get<ProxyPoolConfiguration>();
    config.Validate();
    return config;
});

builder.Services.AddSingleton<GlobalStatus>();
builder.Services.AddSingleton<ProxyParser>();
builder.Services.AddSingleton<Validator>();
builder.Services.AddSingleton<IMongoDatabase>(provider =>
{
    var config = provider.GetRequiredService<ProxyPoolConfiguration>().MongoDB;
    var setting = MongoClientSettings.FromConnectionString(config.Url);
    setting.MaxConnecting = config.MaxConnecting;
    setting.MaxConnectionPoolSize = config.MaxConnectionPoolSize;
    var client = new MongoClient(setting);
    return client.GetDatabase(config.Database);
});
builder.Services.AddSingleton<ProxyRepository>();

builder.Services.AddHostedService<FetchProxyWorker>();
builder.Services.AddHostedService<ValidateWorker>();
builder.Services.AddHostedService<OutdatedWorker>();


ServicePointManager.ReusePort = true;

var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI(); 


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapAreaControllerRoute("api_route", "Api", "Api/{controller}/{action}/{id?}");
    ConventionalRoutingSwaggerGen.UseRoutes(endpoints);
});



app.MapRazorPages();

app.Run();
