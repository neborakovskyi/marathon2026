using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Services.Caching;

public sealed class InMemoryApiResponseCache(IMemoryCache memoryCache) : IApiResponseCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public async Task<T> GetOrCreateAsync<T>(
        string endpoint,
        IDictionary<string, string>? query,
        Func<Task<T>> factory)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(factory);

        var serializedQuery = query is null
            ? "[]"
            : JsonSerializer.Serialize(query.OrderBy(parameter => parameter.Key, StringComparer.Ordinal).ToArray());
        var queryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serializedQuery)));
        var cacheKey = $"api-response:{endpoint}:{queryHash}";

        var cachedValue = await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await factory();
        });

        return cachedValue ?? throw new InvalidOperationException(
            $"The cache factory for '{endpoint}' returned a null value.");
    }
}
