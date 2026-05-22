namespace EleganceStudio.API.Interfaces;

public interface ITokenStore
{
    Task SetAsync(string key, string value, TimeSpan ttl);
    Task<string?> GetAsync(string key);
    Task DeleteAsync(string key);
}
