using EmpireWebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EmpireWebApp.Pages;

public class JoinModel : PageModel
{
    private readonly GameStore _store;

    public JoinModel(GameStore store)
    {
        _store = store;
    }

    [BindProperty]
    public JoinInput Input { get; set; } = new();

    public string? Error { get; set; }

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Input.Code) || string.IsNullOrWhiteSpace(Input.Name))
        {
            Error = "Please enter both fields.";
            return Page();
        }

        if (!_store.TryGetGame(Input.Code.ToUpperInvariant(), out var game) || game == null)
        {
            Error = "Game not found.";
            return Page();
        }

        var existingToken = Request.Cookies["empire_player_token"];
        var player = _store.AddPlayer(game, Input.Name, existingToken);

        Response.Cookies.Append("empire_player_token", player.Token, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        });

        return RedirectToPage("/Game/Play", new { code = game.Code });
    }
}

public class JoinInput
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
