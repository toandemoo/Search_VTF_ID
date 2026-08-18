using Microsoft.AspNetCore.Mvc;
using Search_VTF_ID.Services.Services;

namespace Search_VTF_ID.Controllers;

[ApiController]
[Route("api/vo-sinh")]
public class VoSinhController : ControllerBase
{
    // private readonly VoSinhService _service;
    // private readonly ILogger<VoSinhController> _logger;

    private readonly HoivienService _hoivienService;
    private readonly ILogger<HoivienService> _logger;

    public VoSinhController(
        HoivienService hoivienService,
        ILogger<HoivienService> logger)
    {
        _hoivienService = hoivienService;
        _logger = logger;
    }

    // GET: /api/vo-sinh/all
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var result = await _hoivienService.GetAllAsync();

            return Ok(new
            {
                success = true,
                count = result.Count,
                data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi khi lấy danh sách võ sinh"
            );

            return StatusCode(500, new
            {
                success = false,
                message = "Không thể lấy danh sách võ sinh"
            });
        }
    }

    // GET: /api/vo-sinh? name = toan
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new
            {
                success = false,
                message = "Vui lòng nhập tên võ sinh"
            });
        }

        try
        {
            var result =
                await _hoivienService.SearchAsync(name.Trim());

            return Ok(new
            {
                success = true,
                count = result.Count,
                data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi khi tìm kiếm võ sinh với tên: {Name}",
                name
            );

            return StatusCode(500, new
            {
                success = false,
                message = "Không thể tìm kiếm võ sinh"
            });
        }
    }
}