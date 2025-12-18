using EmpireWebApp.Models;
using EmpireWebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EmpireWebApp.Pages.Game;

[IgnoreAntiforgeryToken]
public class StateModel : PageModel
{
    private readonly GameStore _store;

    public StateModel(GameStore store)
    {
        _store = store;
    }

    [BindProperty(SupportsGet = true, Name = "code")]
    public string Code { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true, Name = "tv")]
    public int Tv { get; set; }

    public IActionResult OnGet()
    {
        Code = Code.ToUpperInvariant();
        if (!_store.TryGetGame(Code.ToUpperInvariant(), out var game) || game == null)
        {
            return NotFound(new { success = false, message = "Game not found" });
        }

        var token = Request.Cookies["empire_player_token"];
        var isTv = Tv == 1;

        PublicState state;
        lock (game.SyncRoot)
        {
            var you = string.IsNullOrEmpty(token) ? null : game.Players.FirstOrDefault(p => p.Token == token);
            var players = game.Players.Select(p => new PlayerView
            {
                Id = p.Id,
                Name = p.Name,
                LeaderId = p.LeaderId,
                PromptSubmitted = !string.IsNullOrWhiteSpace(p.Prompt)
            }).ToList();

            string? promptText = null;
            var totalPrompts = game.Prompts.Count;
            var promptVisible = isTv && !game.PromptsHidden && totalPrompts > 0;
            if (isTv)
            {
                if (promptVisible)
                {
                    promptText = game.Prompts.ElementAtOrDefault(game.PromptIndex)?.Text;
                }
                else if (totalPrompts > 0)
                {
                    promptText = "PROMPT HIDDEN";
                }
            }

            var pending = game.PendingGuess;
            PendingGuessView? pendingView = null;
            if (pending != null)
            {
                pendingView = new PendingGuessView
                {
                    Id = pending.Id,
                    GuesserId = pending.GuesserId,
                    TargetId = pending.TargetId,
                    Status = pending.Status.ToString(),
                    ClaimedOutcome = pending.ClaimedOutcome.ToString(),
                    GuesserName = players.FirstOrDefault(p => p.Id == pending.GuesserId)?.Name ?? "",
                    TargetName = players.FirstOrDefault(p => p.Id == pending.TargetId)?.Name ?? ""
                };
            }

            state = new PublicState
            {
                Code = game.Code,
                Phase = game.Phase.ToString(),
                ActiveGuesserId = game.ActiveGuesserId,
                Players = players,
                PromptIndex = game.PromptIndex,
                TotalPrompts = totalPrompts,
                CycleCount = game.CycleCount,
                PromptsHidden = game.PromptsHidden,
                CurrentPrompt = promptText,
                PromptVisible = promptVisible,
                PendingGuess = pendingView,
                WinnerId = game.WinnerId,
                WinnerName = game.WinnerId != null ? players.FirstOrDefault(p => p.Id == game.WinnerId)?.Name : null,
                YourPlayerId = you?.Id,
                AutoAdvancePrompts = game.AutoAdvancePrompts
            };
        }

        return new JsonResult(state);
    }
}

public class PublicState
{
    public string Code { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string? ActiveGuesserId { get; set; }
    public List<PlayerView> Players { get; set; } = new();
    public int PromptIndex { get; set; }
    public int TotalPrompts { get; set; }
    public int CycleCount { get; set; }
    public bool PromptsHidden { get; set; }
    public string? CurrentPrompt { get; set; }
    public bool PromptVisible { get; set; }
    public PendingGuessView? PendingGuess { get; set; }
    public string? WinnerId { get; set; }
    public string? WinnerName { get; set; }
    public string? YourPlayerId { get; set; }
    public bool AutoAdvancePrompts { get; set; }
}

public class PlayerView
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LeaderId { get; set; }
    public bool PromptSubmitted { get; set; }
}

public class PendingGuessView
{
    public string Id { get; set; } = string.Empty;
    public string GuesserId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string ClaimedOutcome { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string GuesserName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
}
