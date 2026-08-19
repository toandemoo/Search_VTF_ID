using Microsoft.Extensions.Caching.Distributed;
using Search_VTF_ID.Models;

public class HoivienCacheService
{
    private readonly IDistributedCache _cache;

    private const string VERSION_KEY = "hoivien:version";

    // =========================================================
    // MEMORY CACHE
    // =========================================================

    private static List<VoSinh>? _memoryData;

    private static string? _memoryVersion;

    private static readonly object _memoryLock = new();

    public HoivienCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    // =========================================================
    // GET VERSION
    // =========================================================

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

    // =========================================================
    // GET MEMORY DATA
    // =========================================================

    public List<VoSinh>? GetMemoryData(string version)
    {
        lock (_memoryLock)
        {
            if (_memoryData == null)
            {
                return null;
            }

            if (_memoryVersion != version)
            {
                Console.WriteLine(
                    $"MEMORY VERSION MISMATCH: {_memoryVersion} != {version}"
                );

                return null;
            }

            return _memoryData;
        }
    }

    // =========================================================
    // SAVE MEMORY DATA
    // =========================================================

    public void SetMemoryData(
        List<VoSinh> data,
        string version)
    {
        lock (_memoryLock)
        {
            _memoryData = data;
            _memoryVersion = version;
        }

        Console.WriteLine(
            $"MEMORY CACHE SAVED - {data.Count} records - VERSION {version}"
        );
    }

    // =========================================================
    // CLEAR MEMORY
    // =========================================================

    public void ClearMemory()
    {
        lock (_memoryLock)
        {
            _memoryData = null;
            _memoryVersion = null;
        }

        Console.WriteLine("MEMORY CACHE CLEARED");
    }

    // =========================================================
    // SET VERSION ONLY
    //
    // KHÔNG lưu DATA vào Redis
    // =========================================================

    public async Task<bool> SetVersionAsync(
        string version)
    {
        try
        {
            var options =
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromHours(12)
                };

            await _cache.SetStringAsync(
                VERSION_KEY,
                version,
                options
            );

            Console.WriteLine(
                $"REDIS VERSION SAVED: {version}"
            );

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"REDIS SET VERSION ERROR: {ex.Message}"
            );

            // Redis lỗi cũng không làm app chết
            return false;
        }
    }

    // =========================================================
    // CLEAR
    // =========================================================

    public async Task ClearAsync()
    {
        try
        {
            await _cache.RemoveAsync(VERSION_KEY);

            ClearMemory();

            Console.WriteLine(
                "CACHE CLEARED"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"REDIS CLEAR ERROR: {ex.Message}"
            );

            // Redis lỗi vẫn clear memory
            ClearMemory();
        }
    }
}