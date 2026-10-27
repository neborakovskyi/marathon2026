using APIHandler.Services.Caching;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Tests.Services;

public class InMemoryApiResponseCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_SameEndpointAndQuery_UsesCachedValue()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryApiResponseCache(memoryCache);
        var factoryCalls = 0;

        Task<List<string>> CreateResult()
        {
            factoryCalls++;
            return Task.FromResult(new List<string> { "result" });
        }

        IDictionary<string, string> firstQuery = new Dictionary<string, string>
        {
            ["name"] = "Soup",
            ["sortBy"] = "PriceAscending"
        };
        IDictionary<string, string> reorderedQuery = new Dictionary<string, string>
        {
            ["sortBy"] = "PriceAscending",
            ["name"] = "Soup"
        };

        var firstResult = await cache.GetOrCreateAsync("GET /dishes", firstQuery, CreateResult);
        var cachedResult = await cache.GetOrCreateAsync("GET /dishes", reorderedQuery, CreateResult);

        Assert.Same(firstResult, cachedResult);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task GetOrCreateAsync_DifferentQuery_UsesDifferentCachedValue()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryApiResponseCache(memoryCache);
        var firstQuery = new Dictionary<string, string> { ["name"] = "Soup" };
        var secondQuery = new Dictionary<string, string> { ["name"] = "Salad" };

        var firstResult = await cache.GetOrCreateAsync(
            "GET /dishes",
            firstQuery,
            () => Task.FromResult(new List<string> { "soup" }));
        var secondResult = await cache.GetOrCreateAsync(
            "GET /dishes",
            secondQuery,
            () => Task.FromResult(new List<string> { "salad" }));

        Assert.NotSame(firstResult, secondResult);
        Assert.Equal("soup", firstResult[0]);
        Assert.Equal("salad", secondResult[0]);
    }
}
