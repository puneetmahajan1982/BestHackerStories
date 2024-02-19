using BestHackerStories.Cache;
using BestHackerStories.Common;
using System.Collections.Generic;

namespace BestHackerStories.Services
{
    public interface IBestStoriesService
    {
        List<Story> GetTopBestStories(int count);
    }

    public class BestStoriesService : IBestStoriesService
    {
        public BestStoriesService()
        { }

        public List<Story> GetTopBestStories(int count)
        {
            if (BestStoriesCache.IsCacheReady())
            {
                return BestStoriesCache.Stories.OrderByDescending(s => s.Value.score).Take(count).Select(v => v.Value).ToList();
            }
            else
            {
                throw new Exception("Cache not ready, retry after sometime.");
            }
        }
    }
}
