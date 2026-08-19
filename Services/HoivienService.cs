using System.Globalization;
using System.Text;
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

    // =========================================================
    // CHỈ 1 REQUEST ĐƯỢC LOAD CSV TRONG TOÀN BỘ APP
    // =========================================================

    private static readonly SemaphoreSlim _loadLock =
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
        var currentVersion =
            _version.GetVersion();

        // =====================================================
        // 1. CHECK MEMORY
        // =====================================================

        var memoryData =
            _cache.GetMemoryData(currentVersion);

        if (memoryData != null)
        {
            Console.WriteLine(
                $"MEMORY CACHE HIT - {memoryData.Count} records"
            );

            return memoryData;
        }

        // =====================================================
        // 2. MEMORY MISS
        // =====================================================

        Console.WriteLine(
            "MEMORY CACHE MISS - WAITING LOCK"
        );

        await _loadLock.WaitAsync();

        try
        {
            // =================================================
            // 3. DOUBLE CHECK MEMORY
            // =================================================

            Console.WriteLine(
                "LOCK ACQUIRED - CHECK MEMORY AGAIN"
            );

            memoryData =
                _cache.GetMemoryData(currentVersion);

            if (memoryData != null)
            {
                Console.WriteLine(
                    $"MEMORY CACHE HIT AFTER WAIT - {memoryData.Count} records"
                );

                return memoryData;
            }

            // =================================================
            // 4. CHECK REDIS VERSION
            //
            // Redis chỉ dùng để lưu version.
            // KHÔNG lấy data từ Redis.
            // =================================================

            var redisVersion =
                await _cache.GetVersionAsync();

            if (redisVersion == currentVersion)
            {
                Console.WriteLine(
                    "REDIS VERSION MATCH"
                );

                /*
                 * Redis chỉ chứa version.
                 *
                 * Data vẫn phải nằm trong memory.
                 *
                 * Nếu application vừa restart thì memory
                 * sẽ mất => bắt buộc load CSV lại.
                 */
            }
            else
            {
                Console.WriteLine(
                    $"VERSION MISMATCH - REDIS: {redisVersion}, CURRENT: {currentVersion}"
                );
            }

            // =================================================
            // 5. LOAD CSV
            // =================================================

            Console.WriteLine(
                "LOADING DATA FROM CSV"
            );

            var data =
                await LoadDataFromSourceAsync();

            Console.WriteLine(
                $"DATA LOADED - {data.Count} records"
            );

            // =================================================
            // 6. SAVE MEMORY
            //
            // QUAN TRỌNG:
            // save memory trước
            // =================================================

            _cache.SetMemoryData(
                data,
                currentVersion
            );

            // =================================================
            // 7. SAVE VERSION TO REDIS
            //
            // KHÔNG SAVE DATA
            // =================================================

            var versionSaved =
                await _cache.SetVersionAsync(
                    currentVersion
                );

            if (versionSaved)
            {
                Console.WriteLine(
                    "REDIS VERSION SAVED"
                );
            }
            else
            {
                Console.WriteLine(
                    "REDIS VERSION SAVE FAILED - MEMORY CACHE STILL ACTIVE"
                );
            }

            // =================================================
            // 8. RETURN
            // =================================================

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
                NormalizationForm.FormD
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
                NormalizationForm.FormC
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
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true
            );

        using var csvReader =
            new CsvReader(
                reader,
                CultureInfo.InvariantCulture
            );

        // =====================================================
        // BỎ 2 DÒNG ĐẦU
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
        // LOAD RECORDS
        // =====================================================

        var data =
            new List<VoSinh>();

        foreach (
            var record
            in csvReader.GetRecords<VoSinh>())
        {
            data.Add(record);
        }

        return data;
    }
}