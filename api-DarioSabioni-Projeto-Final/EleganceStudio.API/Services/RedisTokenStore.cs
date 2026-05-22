using EleganceStudio.API.Interfaces;
using StackExchange.Redis;

namespace EleganceStudio.API.Services;

public class RedisTokenStore : ITokenStore
{
    private readonly IDatabase _redis;

    public RedisTokenStore(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public async Task SetAsync(string key, string value, TimeSpan ttl)
    {
        await _redis.StringSetAsync(key, value, ttl);
    }

    public async Task<string?> GetAsync(string key)
    {
        var value = await _redis.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task DeleteAsync(string key)
    {
        await _redis.KeyDeleteAsync(key);
    }
}
