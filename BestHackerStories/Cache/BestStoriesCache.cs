using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using BestHackerStories.Common;
using BestHackerStories.Controllers;
using BestHackerStories.Services;

namespace BestHackerStories.Cache
{
    public interface IBestStoriesCache
    {
        void BuildCache(CancellationToken cancellationToken = default);
    }

    public class BestStoriesCache : IBestStoriesCache
    {
        public static ConcurrentDictionary<int, Story> Stories = new ConcurrentDictionary<int, Story>();

        private static bool _cacheBuildStarted = false;
        private static bool _cacheBuildCompleted = false;
        private readonly ILogger<BestStoriesCache> _logger;

        private static object _mutex = new object();
        public IConfiguration _configuration { get; }
        public IHttpClientFactory _httpClientFactory { get; }

        public BestStoriesCache(ILogger<BestStoriesCache> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;

            Initialise();
        }

        private void Initialise()
        {
            if(_cacheBuildStarted) return;

            var cacheRefreshLatency = _configuration.GetValue("BestStoriesConfiguration:CacheRefreshLatencySeconds", string.Empty);

            if (string.IsNullOrEmpty(cacheRefreshLatency) || !int.TryParse(cacheRefreshLatency, out int cacheRefreshLatencySeconds))
            {
                cacheRefreshLatencySeconds = 120000; //default
            }

            Task.Factory.StartNew(() => {
                while (true)
                {
                    if (!IsCacheReady())
                    {
                        _logger.LogInformation($"Building Cache");
                        BuildCache();
                    }
                    else
                    {
                        _logger.LogInformation($"Refreshing Cache");
                        RefreshCache();
                    }
                    
                    Task.Delay(cacheRefreshLatencySeconds).Wait();
                }
            });
        }

        public static bool IsCacheReady()
        {
            return _cacheBuildStarted && _cacheBuildCompleted;
        }

        /// <summary>
        /// Build Cache
        /// </summary>
        public void BuildCache(CancellationToken cancellationToken = default)
        {
            lock (_mutex)
            {
                if (IsCacheReady()) return; // return as cache already built

                try
                {
                    _logger.LogInformation($"Building Cache");
                    _cacheBuildStarted = true;

                    GetBestStoriesAsync(cancellationToken).ContinueWith(t =>
                    {
                        Parallel.ForEach(t.Result, story =>
                                        {
                                            Stories[story.id] = story;
                                        });
                    });

                    _logger.LogInformation($"Cache built successfully");

                    _cacheBuildCompleted = true;
                }
                catch (Exception e)
                {
                    //TODO: WOULD HAVE IMPLEMENTED A RECOVERY MECHANISM IF HAD MORE TIME
                    _logger.LogCritical(e, $"Failed to build cache");
                    _cacheBuildStarted = false;
                    _cacheBuildCompleted = false;
                }
            }
        }

        private void RefreshCache(CancellationToken cancellationToken = default)
        {
            try
            {
                var concurrency = _configuration.GetValue("BestStoriesConfiguration:Concurrency", string.Empty);

                if (string.IsNullOrEmpty(concurrency) || !int.TryParse(concurrency, out int maxDegreeOfParallelism))
                {
                    maxDegreeOfParallelism = 100; //default
                }

                GetBestStoriesAsync(cancellationToken).ContinueWith(t =>
                {
                    Parallel.ForEach(t.Result, new ParallelOptions() { MaxDegreeOfParallelism = maxDegreeOfParallelism }, story =>
                     {
                         Stories[story.id] = story;
                     });
                });

                _logger.LogInformation($"Cache refreshed successfully");
            }
            catch (Exception e)
            {
                //TODO: WOULD HAVE IMPLEMENTED A RECOVERY MECHANISM IF HAD MORE TIME
                _logger.LogCritical(e, $"Failed to Refresh cache");
            }
        }

        /// <summary>
        /// Fetches the best stories from HackerNewsAPI
        /// </summary>
        private async Task<IEnumerable<Story>> GetBestStoriesAsync(CancellationToken cancellationToken)
        {
            IEnumerable<int>? currentBestIds = await GetBestStoryIdsAsync(cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<IEnumerable<Story>>(cancellationToken); ;
            }

            return await GetStoriesByIdAsync(currentBestIds, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Get the IDs of the best stories from HackerNewsAPI.
        /// </summary>
        private async Task<IEnumerable<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using HttpClient httpClient = _httpClientFactory.CreateClient(Constants.BEST_STORIES_CACHE_API);

                using HttpResponseMessage response = await httpClient.GetAsync("beststories.json", cancellationToken)
                    .ConfigureAwait(false);

                return await JsonSerializer.DeserializeAsync<IEnumerable<int>>(
                    await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                    JsonSerializerOptions.Default, cancellationToken)
                    .ConfigureAwait(false) ?? throw new NullReferenceException();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return await Task.FromException<IEnumerable<int>>(ex);
            }
        }

        /// <summary>
        /// Get each story in the collection by ID from HackerNewsApi.
        /// </summary>
        private async Task<IEnumerable<Story>> GetStoriesByIdAsync(IEnumerable<int> bestIds, CancellationToken cancellationToken)
        {
            var concurrency = _configuration.GetValue("BestStoriesConfiguration:Concurrency", string.Empty);

            if (string.IsNullOrEmpty(concurrency) || !int.TryParse(concurrency, out int maxDegreeOfParallelism))
            {
                maxDegreeOfParallelism = 100; //default
            }
            List<Task<Story>> bestStories = new List<Task<Story>>();

            Parallel.ForEach(bestIds, new ParallelOptions() { CancellationToken = cancellationToken, MaxDegreeOfParallelism = maxDegreeOfParallelism }, (id =>
            {
                bestStories.Add(GetStoryAsync(id, cancellationToken));
            }));

            try
            {
                _logger.LogInformation($"Waiting for {bestStories.Count} story details.");
                return await Task.WhenAll(bestStories);
            }
            catch (Exception ex)
            {
                var aggregateExceptions = bestStories.Where(t => t.Exception != null).Select(t => t.Exception);

                if (aggregateExceptions.Any())
                {
                    foreach (AggregateException? aggregateException in aggregateExceptions)
                    {
                        aggregateException?.Handle((x) =>
                        {
                            _logger.LogError(x, x.Message);
                            return true;
                        });
                    }
                }

                return await Task.FromException<IEnumerable<Story>>(ex);
            }
        }

        /// <summary>
        /// Get the story by ID from HackerNewsAPI.
        /// </summary>
        private async Task<Story> GetStoryAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                using HttpClient httpClient = _httpClientFactory.CreateClient(Constants.BEST_STORIES_CACHE_API);

                using HttpResponseMessage response = await httpClient.GetAsync($"item/{id}.json", cancellationToken);

                return await JsonSerializer.DeserializeAsync<Story>(
                    await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                    JsonSerializerOptions.Default, cancellationToken).ConfigureAwait(false) ?? throw new NullReferenceException();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"GetStoryAsync({id})");

                return await Task.FromException<Story>(ex);
            }
        }
    }
}
