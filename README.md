# Best Hacker Stories
Solution is written in Visual Studion 2022 Pro.
WebAPI is ASP.NET Core.

### Assumptions
- No Authentication required
- No means to query delta records
- The structure of data returned from the HackerNews API will not change in this version.

## How to run the application
Clone the repo and swith to master branch to get the code. 
Code is written in Visual Studion 2022 Pro.

To test you can use swagger 
  - Select BestHackerStories v1 definition.
  - TryOut /api/BestStories endpoint, you will get option to input count of best stories.

    
    ![image](https://github.com/puneetmahajan1982/BestHackerStories/assets/26072941/057c1c3d-fe18-48b3-9345-2486f52cc850)


    ![image](https://github.com/puneetmahajan1982/BestHackerStories/assets/26072941/0e3eb4f6-0afb-46a1-b78a-acb643957542)


## Implementation Details
- WebApi will build and maintain a cache of stories. When cache is build it will periodically update the cache after a pre-configured timeframe defined in the configuration.
- To prevent overloading of API when querying large resultset, means of concurrency is applied which will query and process smaller sets of data.
- Some resiliency is implemented where code will retry to build cache if failed initially.

## If I had more time
- perform load testing on larger dataset
- implemented resilient caching to prevent data overload and manage timeouts
- implemented some level of authentication to restrict access
- implemented unit tests
