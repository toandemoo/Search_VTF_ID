using CsvHelper;
using Microsoft.Extensions.Caching.Memory;
using Search_VTF_ID.Models;
using Search_VTF_ID.Maps;
using System.Globalization;

namespace Search_VTF_ID.Services.Services;

public class VoSinhService
{
    private const string CSV_URL =
        "https://tkdvn1996.info/0.key/ds_hoivien.csv";

    private const string CACHE_KEY =
        "ds_hoivien";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    private readonly SemaphoreSlim _loadLock =
        new(1, 1);

    public VoSinhService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache)
    {
        _httpClientFactory =
            httpClientFactory;

        _cache = cache;
    }

    public async Task<List<VoSinh>> GetAllAsync()
    {
        // Cache
        if (_cache.TryGetValue(
            CACHE_KEY,
            out List<VoSinh>? cached))
        {
            return cached!;
        }

        await _loadLock.WaitAsync();

        try
        {
            // Kiểm tra cache lần 2
            if (_cache.TryGetValue(
                CACHE_KEY,
                out cached))
            {
                return cached!;
            }

            var client =
                _httpClientFactory.CreateClient();

            client.Timeout =
                TimeSpan.FromSeconds(60);

            using var stream =
                await client.GetStreamAsync(
                    CSV_URL
                );

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

            // Bỏ dòng:
            // DANH SÁCH HỘI VIÊN
            csvReader.Read();

            // Bỏ dòng trống
            csvReader.Read();

            // Đọc dòng header
            csvReader.Read();

            csvReader.Context.RegisterClassMap<VoSinhMap>();

            var students =
                csvReader
                    .GetRecords<VoSinh>()
                    .ToList();

            // Cache 10 phút
            _cache.Set(
                CACHE_KEY,
                students,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromMinutes(10)
                }
            );

            return students;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task<List<VoSinh>> SearchAsync(
        string name)
    {
        var students =
            await GetAllAsync();

        var keyword =
            RemoveVietnameseTone(name)
                .ToLowerInvariant()
                .Trim();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        return students
            .Where(x =>
                RemoveVietnameseTone(x.HoTen)
                    .ToLowerInvariant()
                    .Contains(keyword)
            )
            .ToList();
    }

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

        var result = new string(
            normalized
                .Where(c =>
                    CharUnicodeInfo
                        .GetUnicodeCategory(c)
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
}