using System.Globalization;
using CsvHelper;
using Search_VTF_ID.Maps;
using Search_VTF_ID.Models;

public class HoivienService
{
    private const string DATA_URL =
        "https://tkdvn1996.info/0.key/ds_hoivien.csv";

    private readonly HoivienCacheService _cache;
    private readonly DataVersionService _version;
    private readonly IHttpClientFactory _httpClientFactory;

    // =========================
    // CHỈ 1 request được LOAD CSV
    // =========================

    private readonly SemaphoreSlim _loadLock =
        new(1, 1);

    public HoivienService(
        HoivienCacheService cache,
        DataVersionService version,
        IHttpClientFactory httpClientFactory)
    {
        _cache = cache;
        _version = version;
        _httpClientFactory = httpClientFactory;
    }

    // =========================================================
    // GET ALL
    // =========================================================

    public async Task<List<VoSinh>> GetAllAsync()
    {
        var currentVersion = _version.GetVersion();

        // =====================================================
        // 1. KIỂM TRA CACHE
        // =====================================================

        var cacheVersion =
            await _cache.GetVersionAsync();

        if (cacheVersion == currentVersion)
        {
            var cached =
                await _cache.GetDataAsync();

            if (cached != null)
            {
                Console.WriteLine(
                    $"CACHE HIT - {cached.Count} records"
                );

                return cached;
            }
        }

        // =====================================================
        // 2. CACHE MISS
        // =====================================================

        Console.WriteLine(
            "CACHE MISS - WAITING LOCK"
        );

        await _loadLock.WaitAsync();

        try
        {
            // =================================================
            // 3. DOUBLE CHECK
            //
            // Trong lúc request này chờ lock,
            // request khác có thể đã load xong.
            // =================================================

            Console.WriteLine(
                "LOCK ACQUIRED - CHECK CACHE AGAIN"
            );

            cacheVersion =
                await _cache.GetVersionAsync();

            if (cacheVersion == currentVersion)
            {
                var cached =
                    await _cache.GetDataAsync();

                if (cached != null)
                {
                    Console.WriteLine(
                        $"CACHE HIT AFTER WAIT - {cached.Count} records"
                    );

                    return cached;
                }
            }

            // =================================================
            // 4. LOAD CSV
            // =================================================

            Console.WriteLine(
                "CACHE MISS - LOAD DATA"
            );

            var data =
                await LoadDataFromSourceAsync();

            Console.WriteLine(
                $"DATA LOADED - {data.Count} records"
            );

            // =================================================
            // 5. SAVE REDIS
            // =================================================

            var saved =
                await _cache.SetDataAsync(
                    data,
                    currentVersion
                );

            if (saved)
            {
                Console.WriteLine(
                    "CACHE SAVED"
                );
            }
            else
            {
                Console.WriteLine(
                    "CACHE SAVE FAILED - USING MEMORY DATA"
                );
            }

            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"LOAD DATA ERROR: {ex}"
            );

            throw;
        }
        finally
        {
            // =================================================
            // 6. BẮT BUỘC RELEASE
            // =================================================

            _loadLock.Release();

            Console.WriteLine(
                "LOCK RELEASED"
            );
        }
    }

    // =========================================================
    // SEARCH
    // =========================================================

    public async Task<List<VoSinh>> SearchAsync(
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var keyword =
            RemoveVietnameseTone(name)
                .ToLowerInvariant()
                .Trim();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        var students =
            await GetAllAsync();

        return students
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.HoTen)
                &&
                RemoveVietnameseTone(x.HoTen)
                    .ToLowerInvariant()
                    .Contains(keyword)
            )
            .ToList();
    }

    // =========================================================
    // REMOVE VIETNAMESE TONE
    // =========================================================

    private static string RemoveVietnameseTone(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var normalized =
            text.Normalize(
                System.Text.NormalizationForm.FormD
            );

        var result =
            new string(
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

    // =========================================================
    // LOAD CSV
    // =========================================================

    private async Task<List<VoSinh>>
        LoadDataFromSourceAsync()
    {
        var client =
            _httpClientFactory.CreateClient();

        client.Timeout =
            TimeSpan.FromSeconds(60);

        Console.WriteLine(
            $"DOWNLOADING CSV: {DATA_URL}"
        );

        using var response =
            await client.GetAsync(
                DATA_URL,
                HttpCompletionOption.ResponseHeadersRead
            );

        response.EnsureSuccessStatusCode();

        Console.WriteLine(
            $"CSV STATUS: {(int)response.StatusCode}"
        );

        await using var stream =
            await response.Content.ReadAsStreamAsync();

        using var reader =
            new StreamReader(
                stream,
                System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true
            );

        using var csvReader =
            new CsvReader(
                reader,
                CultureInfo.InvariantCulture
            );

        // =====================================================
        // BỎ:
        //
        // DANH SÁCH HỘI VIÊN
        //
        // dòng trống
        // =====================================================

        csvReader.Read();
        csvReader.Read();

        // =====================================================
        // HEADER
        // =====================================================

        csvReader.Read();

        csvReader.Context
            .RegisterClassMap<VoSinhMap>();

        // =====================================================
        // LOAD RECORD
        // =====================================================

        var data =
            new List<VoSinh>();

        foreach (
            var record
            in csvReader.GetRecords<VoSinh>())
        {
            data.Add(record);

            // Không tạo ToList()
            // Không giữ IEnumerable trung gian
        }

        return data;
    }
}