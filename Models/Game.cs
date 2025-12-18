using System.Collections.Concurrent;

namespace EmpireWebApp.Models;

public class Game
{
    public string Code { get; set; } = string.Empty;
    public List<Player> Players { get; set; } = new();
    public List<PromptItem> Prompts { get; set; } = new();
    public int PromptIndex { get; set; }
    public int CycleCount { get; set; }
    public bool AutoAdvancePrompts { get; set; } = true;
    public string? ActiveGuesserId { get; set; }
    public PendingGuess? PendingGuess { get; set; }
    public GamePhase Phase { get; set; } = GamePhase.Lobby;
    public string? WinnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public object SyncRoot { get; } = new();

    public bool PromptsHidden => CycleCount >= 2;
}

public class Player
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public string? Prompt { get; set; }
    public string? LeaderId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class PromptItem
{
    public string PlayerId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class PendingGuess
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GuesserId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public GuessOutcome ClaimedOutcome { get; set; }
    public PendingGuessStatus Status { get; set; } = PendingGuessStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum PendingGuessStatus
{
    Pending,
    Confirmed,
    Denied,
    Cancelled
}

public enum GuessOutcome
{
    Correct,
    Wrong
}

public enum GamePhase
{
    Lobby,
    Playing,
    Finished
}
