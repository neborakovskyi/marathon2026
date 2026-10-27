using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Services.Caching;

public interface IApiResponseCache
{
    Task<T> GetOrCreateAsync<T>(
        string endpoint,
        IDictionary<string, string>? query,
        Func<Task<T>> factory)
        where T : class;
}
