using System.Globalization;
using CsvHelper;
using Search_VTF_ID.Maps;
using Search_VTF_ID.Models;

public class HoivienService
{
    private readonly HoivienCacheService _cache;
    private readonly DataVersionService _version;
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public HoivienService(
        HoivienCacheService cache,
        DataVersionService version,
        IHttpClientFactory httpClientFactory)
    {
        _cache = cache;
        _version = version;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<VoSinh>> GetAllAsync()
    {
        var currentVersion = _version.GetVersion();

        // =========================
        // 1. Kiểm tra cache
        // =========================
        var cacheVersion = await _cache.GetVersionAsync();

        if (cacheVersion == currentVersion)
        {
            var cached = await _cache.GetDataAsync();

            if (cached != null)
            {
                Console.WriteLine("CACHE HIT");
                return cached;
            }
        }

        Console.WriteLine("CACHE MISS - WAITING LOCK");

        // =========================
        // 2. Chỉ cho 1 request load CSV
        // =========================
        await _loadLock.WaitAsync();

        try
        {
            // =========================
            // 3. QUAN TRỌNG:
            //    Kiểm tra cache lại sau khi
            //    lấy được lock
            // =========================
            cacheVersion = await _cache.GetVersionAsync();

            if (cacheVersion == currentVersion)
            {
                var cached = await _cache.GetDataAsync();

                if (cached != null)
                {
                    Console.WriteLine("CACHE HIT AFTER WAIT");
                    return cached;
                }
            }

            // =========================
            // 4. Chỉ request đầu tiên
            //    mới chạy tới đây
            // =========================
            Console.WriteLine("CACHE MISS - LOAD DATA");

            var data = await LoadDataFromSourceAsync();

            Console.WriteLine(
                $"DATA LOADED: {data.Count} records"
            );

            // =========================
            // 5. Lưu Redis
            // =========================
            await _cache.SetDataAsync(
                data,
                currentVersion
            );

            Console.WriteLine("CACHE SAVED");

            return data;
        }
        finally
        {
            // =========================
            // 6. BẮT BUỘC RELEASE LOCK
            // =========================
            _loadLock.Release();
        }
    }

    public async Task<List<VoSinh>> SearchAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var keyword = RemoveVietnameseTone(name)
            .ToLowerInvariant()
            .Trim();

        var students = await GetAllAsync();

        return students
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.HoTen) &&
                RemoveVietnameseTone(x.HoTen)
                    .ToLowerInvariant()
                    .Contains(keyword)
            )
            .ToList();
    }

    private static string RemoveVietnameseTone(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var normalized = text.Normalize(
            System.Text.NormalizationForm.FormD
        );

        var result = new string(
            normalized
                .Where(c =>
                    CharUnicodeInfo.GetUnicodeCategory(c)
                    != UnicodeCategory.NonSpacingMark
                )
                .ToArray()
        );

        return result
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Normalize(
                System.Text.NormalizationForm.FormC
            );
    }

    private async Task<List<VoSinh>> LoadDataFromSourceAsync()
    {
        var client = _httpClientFactory.CreateClient();

        client.Timeout = TimeSpan.FromSeconds(60);

        using var stream = await client.GetStreamAsync(
            "https://tkdvn1996.info/0.key/ds_hoivien.csv"
        );

        using var reader = new StreamReader(
            stream,
            System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true
        );

        using var csvReader = new CsvReader(
            reader,
            CultureInfo.InvariantCulture
        );

        // =========================
        // Bỏ:
        // DANH SÁCH HỘI VIÊN
        // =========================
        csvReader.Read();

        // =========================
        // Bỏ dòng trống
        // =========================
        csvReader.Read();

        // =========================
        // Đọc header
        // =========================
        csvReader.Read();

        csvReader.Context.RegisterClassMap<VoSinhMap>();

        // =========================
        // Đọc từng record
        // =========================
        var data = new List<VoSinh>();

        foreach (var record in csvReader.GetRecords<VoSinh>())
        {
            data.Add(record);
        }

        return data;
    }
}