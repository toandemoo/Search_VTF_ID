using Microsoft.AspNetCore.Mvc.RazorPages;
using Search_VTF_ID.Models;

namespace Search_VTF_ID.Pages;

// public class IndexModel : PageModel
// {
//     public void OnGet()
//     {
//     }
// }


public class IndexModel : PageModel
{
    private readonly HoivienService _hoivienService;

    public List<VoSinh> Hoivien { get; set; }
        = new();

    public IndexModel(
        HoivienService hoivienService)
    {
        _hoivienService = hoivienService;
    }

    public async Task OnGetAsync()
    {
        Hoivien = await _hoivienService.GetAllAsync();
    }
}

