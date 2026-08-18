using System.Globalization;
using CsvHelper;
using Search_VTF_ID.Maps;
using Search_VTF_ID.Models;



public class HoivienService
{
    private readonly HoivienCacheService _cache;
    private readonly DataVersionService _version;
    private readonly IHttpClientFactory _httpClientFactory;
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
        var currentVersion =
            _version.GetVersion();

        var cacheVersion =
            await _cache.GetVersionAsync();

        if (cacheVersion == currentVersion)
        {
            var cached =
                await _cache.GetDataAsync();

            if (cached != null)
            {
                Console.WriteLine(
                    "CACHE HIT"
                );

                return cached;
            }
        }

        Console.WriteLine(
            "CACHE MISS - LOAD DATA"
        );

        var data = await LoadDataFromSourceAsync();

        await _cache.SetDataAsync(
            data,
            currentVersion
        );

        return data;
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

    private async Task<List<VoSinh>> LoadDataFromSourceAsync()
    {
        var client =
                _httpClientFactory.CreateClient();

        client.Timeout =
            TimeSpan.FromSeconds(60);

        using var stream =
            await client.GetStreamAsync(
                "https://tkdvn1996.info/0.key/ds_hoivien.csv"
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


        var data =
            csvReader.GetRecords<VoSinh>()
            .ToList();

        return await Task.FromResult(data);
    }
}