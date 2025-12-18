using EmpireWebApp.Services;
using Microsoft.AspNetCore.Http;
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
            var hostToken = Guid.NewGuid().ToString("N");
            var game = _store.CreateGame(hostToken);

            Response.Cookies.Append($"empire_host_{game.Code}", hostToken, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
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
