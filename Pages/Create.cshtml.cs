using EmpireWebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EmpireWebApp.Pages;

public class CreateModel : PageModel
{
    private readonly GameStore _store;

    public CreateModel(GameStore store)
    {
        _store = store;
    }

    [BindProperty]
    public string? Error { get; set; }

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        try
        {
            var game = _store.CreateGame();
            Response.Cookies.Append("empire_host_token", game.HostToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
            return RedirectToPage("/Game/Play", new { code = game.Code });
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}
