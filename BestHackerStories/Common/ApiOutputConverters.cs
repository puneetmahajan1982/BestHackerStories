namespace BestHackerStories.Common
{
    public class ApiOutputConverters
    {
        public static Func<Story, object> StoryOutput()
        {
            return s => new
            {
                title = s.title,
                uri = s.url,
                postedBy = s.by,
                time = s.time,
                score = s.score,
                commentCount = s.descendants
            };
        }
    }
}
