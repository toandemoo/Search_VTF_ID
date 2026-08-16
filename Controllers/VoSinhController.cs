using Microsoft.AspNetCore.Mvc;
using Search_VTF_ID.Services.Services;

namespace Search_VTF_ID.Controllers;

[ApiController]
[Route("api/vo-sinh")]
public class VoSinhController : ControllerBase
{
    private readonly VoSinhService _service;

    public VoSinhController(VoSinhService service)
    {
        _service = service;
    }

    // GET: /api/vo-sinh/all
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var result = await _service.GetAllAsync();

            return Ok(new
            {
                success = true,
                count = result.Count,
                data = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Không thể lấy danh sách võ sinh",
                error = ex.Message
            });
        }
    }

    // GET: /api/vo-sinh?name=toan
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
            var result = await _service.SearchAsync(name);

            return Ok(new
            {
                success = true,
                count = result.Count,
                data = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Không thể tìm kiếm võ sinh",
                error = ex.Message
            });
        }
    }
}