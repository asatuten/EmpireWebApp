using EmpireWebApp.Hubs;
using EmpireWebApp.Models;
using EmpireWebApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace EmpireWebApp.Pages.Game;

[IgnoreAntiforgeryToken]
    public class PlayModel : PageModel
    {
        private readonly GameStore _store;
        private readonly IHubContext<EmpireHub> _hub;

    public PlayModel(GameStore store, IHubContext<EmpireHub> hub)
    {
        _store = store;
        _hub = hub;
    }

    [BindProperty(SupportsGet = true, Name = "code")]
    public string Code { get; set; } = string.Empty;

    public string PlayerName { get; set; } = "";
    public bool IsHost { get; set; }

    public IActionResult OnGet()
    {
        Code = Code.ToUpperInvariant();
        if (!_store.TryGetGame(Code.ToUpperInvariant(), out var game) || game == null)
        {
            return RedirectToPage("/Join");
        }

        var token = Request.Cookies["empire_player_token"];
        var player = !string.IsNullOrEmpty(token) ? _store.FindPlayerByToken(game, token) : null;
        PlayerName = player?.Name ?? "Unknown";
        IsHost = IsHostUser(game);
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitPrompt([FromBody] PromptRequest request)
    {
        if (!TryGetGame(out var game, out var errorResult))
        {
            return errorResult!;
        }

        var player = GetPlayer(game!);
        if (player == null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { success = false, message = "Player not found" });
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { success = false, message = "Prompt required" });
        }

        _store.SubmitPrompt(game!, player.Id, request.Prompt);
        await BroadcastUpdate();
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostClaim([FromBody] ClaimRequest request)
    {
        if (!TryGetGame(out var game, out var errorResult))
        {
            return errorResult!;
        }

        var player = GetPlayer(game!);
        if (player == null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { success = false, message = "Player not found" });
        }

        if (!_store.ClaimGuess(game!, player.Id, request.TargetId, request.Outcome))
        {
            return BadRequest(new { success = false, message = "Could not submit guess" });
        }

        await BroadcastUpdate();
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostConfirm([FromBody] ConfirmRequest request)
    {
        if (!TryGetGame(out var game, out var errorResult))
        {
            return errorResult!;
        }

        var player = GetPlayer(game!);
        if (player == null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { success = false, message = "Player not found" });
        }

        if (!_store.ConfirmPending(game!, player.Id, request.Confirm))
        {
            return BadRequest(new { success = false, message = "Unable to respond to guess" });
        }

        await BroadcastUpdate();
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostStart()
    {
        if (!TryGetGame(out var game, out var errorResult))
        {
            return errorResult!;
        }

        if (!IsHostUser(game!))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Only host can start" });
        }

        if (!_store.StartGame(game!))
        {
            return BadRequest(new { success = false, message = "Need at least one player" });
        }

        await BroadcastUpdate();
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostNextPrompt()
    {
        if (!TryGetGame(out var game, out var errorResult))
        {
            return errorResult!;
        }

        _store.AdvancePrompt(game!);
        await BroadcastUpdate();
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostToggleAuto([FromBody] ToggleRequest request)
    {
        if (!TryGetGame(out var game, out var errorResult))
        {
            return errorResult!;
        }

        if (!IsHostUser(game!))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Only host can toggle" });
        }

        _store.SetAutoAdvance(game!, request.Enabled);
        await BroadcastUpdate();
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostReset()
    {
        if (!TryGetGame(out var game, out var errorResult))
        {
            return errorResult!;
        }

        if (!IsHostUser(game!))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Only host can reset" });
        }

        _store.ResetGame(game!);
        PlayerName = "";
        IsHost = false;
        await BroadcastUpdate();
        return new JsonResult(new { success = true });
    }

    private bool TryGetGame(out Models.Game? game, out IActionResult? errorResult)
    {
        if (!_store.TryGetGame(Code.ToUpperInvariant(), out game) || game == null)
        {
            errorResult = NotFound(new { success = false, message = "Game not found" });
            return false;
        }

        errorResult = null;
        return true;
    }

    private Player? GetPlayer(Models.Game game)
    {
        var token = Request.Cookies["empire_player_token"];
        return string.IsNullOrEmpty(token) ? null : _store.FindPlayerByToken(game, token);
    }

    private bool IsHostUser(Models.Game game)
    {
        var hostToken = Request.Cookies["empire_host_token"];
        return !string.IsNullOrEmpty(hostToken) && string.Equals(hostToken, game.HostToken, StringComparison.Ordinal);
    }

    private Task BroadcastUpdate()
    {
        return _hub.Clients.Group(EmpireHub.GroupName(Code)).SendAsync("GameUpdated");
    }
}

public class PromptRequest
{
    public string Prompt { get; set; } = string.Empty;
}

public class ClaimRequest
{
    public string TargetId { get; set; } = string.Empty;
    public GuessOutcome Outcome { get; set; }
}

public class ConfirmRequest
{
    public bool Confirm { get; set; }
}

public class ToggleRequest
{
    public bool Enabled { get; set; }
}
