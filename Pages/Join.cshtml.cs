using EmpireWebApp.Hubs;
using EmpireWebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace EmpireWebApp.Pages;

public class JoinModel : PageModel
{
    private readonly GameStore _store;
    private readonly IHubContext<EmpireHub> _hub;

    public JoinModel(GameStore store, IHubContext<EmpireHub> hub)
    {
        _store = store;
        _hub = hub;
    }

    [BindProperty]
    public JoinInput Input { get; set; } = new();

    public string? Error { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPost()
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

        string? existingToken = null;
        if (!Input.NewDevice)
        {
            existingToken = Request.Cookies["empire_player_token"];
        }

        var player = _store.AddPlayer(game, Input.Name, existingToken);

        Response.Cookies.Append("empire_player_token", player.Token, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        });

        await BroadcastUpdate(game.Code);

        return RedirectToPage("/Game/Play", new { code = game.Code });
    }

    private Task BroadcastUpdate(string code)
    {
        return _hub.Clients.Group(EmpireHub.GroupName(code)).SendAsync("GameUpdated");
    }
}

public class JoinInput
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool NewDevice { get; set; }
}
