using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Search_VTF_ID.Models;

public class HoivienCacheService
{
    private readonly IDistributedCache _cache;

    private const string DATA_KEY = "hoivien:data";
    private const string VERSION_KEY = "hoivien:version";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HoivienCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    // =========================
    // GET VERSION
    // =========================

    public async Task<string?> GetVersionAsync()
    {
        try
        {
            return await _cache.GetStringAsync(VERSION_KEY);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"REDIS GET VERSION ERROR: {ex.Message}"
            );

            return null;
        }
    }

    // =========================
    // GET DATA
    // =========================

    public async Task<List<VoSinh>?> GetDataAsync()
    {
        try
        {
            var json = await _cache.GetStringAsync(DATA_KEY);

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<List<VoSinh>>(
                json,
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"REDIS GET DATA ERROR: {ex.Message}"
            );

            return null;
        }
    }

    // =========================
    // SET DATA
    // =========================

    public async Task<bool> SetDataAsync(
        List<VoSinh> data,
        string version)
    {
        try
        {
            var json = JsonSerializer.Serialize(
                data,
                JsonOptions
            );

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromHours(12)
            };

            await _cache.SetStringAsync(
                DATA_KEY,
                json,
                options
            );

            await _cache.SetStringAsync(
                VERSION_KEY,
                version,
                options
            );

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"REDIS SET ERROR: {ex.Message}"
            );

            return false;
        }
    }

    // =========================
    // CLEAR
    // =========================

    public async Task ClearAsync()
    {
        try
        {
            await _cache.RemoveAsync(DATA_KEY);
            await _cache.RemoveAsync(VERSION_KEY);

            Console.WriteLine("REDIS CACHE CLEARED");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"REDIS CLEAR ERROR: {ex.Message}"
            );
        }
    }
}