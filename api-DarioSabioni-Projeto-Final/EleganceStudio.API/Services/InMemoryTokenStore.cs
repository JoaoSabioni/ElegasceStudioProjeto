using EleganceStudio.API.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace EleganceStudio.API.Services;

public class InMemoryTokenStore : ITokenStore
{
    private readonly IMemoryCache _cache;

    public InMemoryTokenStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task SetAsync(string key, string value, TimeSpan ttl)
    {
        _cache.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key)
    {
        return Task.FromResult(_cache.Get<string>(key));
    }

    public Task DeleteAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}
