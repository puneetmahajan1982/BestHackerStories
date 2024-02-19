using BestHackerStories.Cache;
using BestHackerStories.Common;
using BestHackerStories.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BestHackerStories.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BestStoriesController : ControllerBase
    {
        private readonly ILogger<BestStoriesController> _logger;
        private readonly IBestStoriesService _bestStoriesService;

        public BestStoriesController(ILogger<BestStoriesController> logger, IBestStoriesService bestStoriesService)
        {
            _logger = logger;
            _bestStoriesService = bestStoriesService;
        }

        [HttpGet(Name = "GetBestStories")]
        public async Task<IResult> Get(int count)
        {
            try
            {
                return Results.Ok(_bestStoriesService.GetTopBestStories(count).Select(ApiOutputConverters.StoryOutput()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
