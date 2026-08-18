using Microsoft.Extensions.Caching.Memory;
using Search_VTF_ID.Models;
using Search_VTF_ID.Services.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// builder.Services.AddStackExchangeRedisCache(options =>
// {
//     options.Configuration =
//         builder.Configuration.GetConnectionString("Redis");

//     options.InstanceName = "VTF:";
// });


builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "redis://red-da22hijncjis738daiug:6379";
});

builder.Services.AddScoped<VoSinhService>();
builder.Services.AddScoped<HoivienCacheService>();
builder.Services.AddSingleton<DataVersionService>();
builder.Services.AddScoped<HoivienService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();
app.MapRazorPages();

app.Run();