using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Search_VTF_ID.Models;

public class HoivienCacheService
{
    private readonly IDistributedCache _cache;

    private const string DATA_KEY = "hoivien:data";
    private const string VERSION_KEY = "hoivien:version";

    public HoivienCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string?> GetVersionAsync()
    {
        return await _cache.GetStringAsync(VERSION_KEY);
    }

    public async Task<List<VoSinh>?> GetDataAsync()
    {
        var json = await _cache.GetStringAsync(DATA_KEY);

        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<List<VoSinh>>(json);
    }

    public async Task SetDataAsync(
        List<VoSinh> data,
        string version)
    {
        var json = JsonSerializer.Serialize(data);

        await _cache.SetStringAsync(
            DATA_KEY,
            json
        );

        await _cache.SetStringAsync(
            VERSION_KEY,
            version
        );
    }

    public async Task ClearAsync()
    {
        await _cache.RemoveAsync(DATA_KEY);
        await _cache.RemoveAsync(VERSION_KEY);
    }
}