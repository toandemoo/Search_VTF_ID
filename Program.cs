using Microsoft.Extensions.Caching.Memory;
using Search_VTF_ID.Models;
using Search_VTF_ID.Services.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Services.AddScoped<VoSinhService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();
app.MapRazorPages();

app.Run();