using BestHackerStories.Cache;
using BestHackerStories.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<BestStoriesConfiguration>(builder.Configuration.GetSection("BestStoriesConfiguration"));

builder.Services.AddHttpClient(BestHackerStories.Common.Constants.BEST_STORIES_CACHE_API, (serviceProvider, httpClient) =>
{
    if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));
    if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));

    IOptions<BestStoriesConfiguration>? bestStoriesConfiguration = serviceProvider.GetService<IOptions<BestStoriesConfiguration>>();

    if (bestStoriesConfiguration == null)
    {
        throw new NullReferenceException(nameof(bestStoriesConfiguration));
    }

    httpClient.BaseAddress = new Uri(bestStoriesConfiguration.Value.HackerNewsApiUrl ?? throw new ArgumentNullException(bestStoriesConfiguration.Value.HackerNewsApiUrl));
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };

    return handler;
});

builder.Services.AddScoped<IBestStoriesService, BestStoriesService>();
builder.Services.AddSingleton<IBestStoriesCache, BestStoriesCache>();

var app = builder.Build();

IBestStoriesCache bestStoriesCache = app.Services.GetRequiredService<IBestStoriesCache>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
