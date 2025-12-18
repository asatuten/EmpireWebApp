using System.Collections.Concurrent;
using EmpireWebApp.Models;

namespace EmpireWebApp.Services;

public class GameStore
{
    private readonly ConcurrentDictionary<string, Game> _games = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = Random.Shared;
    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public Game CreateGame(string hostToken)
    {
        var code = GenerateCode();
        var game = new Game
        {
            Code = code,
            HostToken = hostToken,
            Players = new List<Player>(),
            Prompts = new List<PromptItem>(),
            Phase = GamePhase.Lobby,
            PromptIndex = 0,
            CycleCount = 0,
            AutoAdvancePrompts = true
        };

        _games[code] = game;
        return game;
    }

    public bool TryGetGame(string code, out Game? game) => _games.TryGetValue(code, out game);

    public Player AddPlayer(Game game, string name, string? existingToken)
    {
        lock (game.SyncRoot)
        {
            if (!string.IsNullOrWhiteSpace(existingToken))
            {
                var existing = game.Players.FirstOrDefault(p => p.Token == existingToken);
                if (existing != null)
                {
                    existing.Name = name;
                    return existing;
                }
            }

            var player = new Player
            {
                Name = name.Trim(),
                Token = Guid.NewGuid().ToString("N")
            };

            game.Players.Add(player);
            return player;
        }
    }

    public Player? FindPlayerByToken(Game game, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        lock (game.SyncRoot)
        {
            return game.Players.FirstOrDefault(p => p.Token == token);
        }
    }

    public bool SubmitPrompt(Game game, string playerId, string prompt)
    {
        lock (game.SyncRoot)
        {
            var player = game.Players.FirstOrDefault(p => p.Id == playerId);
            if (player == null)
            {
                return false;
            }

            player.Prompt = prompt.Trim();
            return true;
        }
    }

    public bool StartGame(Game game)
    {
        lock (game.SyncRoot)
        {
            if (game.Players.Count == 0)
            {
                return false;
            }

            foreach (var player in game.Players)
            {
                player.LeaderId = null;
            }

            game.Prompts = game.Players
                .Where(p => !string.IsNullOrWhiteSpace(p.Prompt))
                .Select(p => new PromptItem { PlayerId = p.Id, Text = p.Prompt!.Trim() })
                .OrderBy(_ => _random.Next())
                .ToList();

            game.PromptIndex = 0;
            game.CycleCount = 0;
            game.PendingGuess = null;
            game.WinnerId = null;
            game.Phase = GamePhase.Playing;

            var shuffledPlayers = game.Players.OrderBy(_ => _random.Next()).ToList();
            game.ActiveGuesserId = shuffledPlayers.First().Id;
            return true;
        }
    }

    public void ResetGame(Game game)
    {
        lock (game.SyncRoot)
        {
            foreach (var player in game.Players)
            {
                player.LeaderId = null;
                player.Prompt = null;
            }

            game.Prompts.Clear();
            game.PromptIndex = 0;
            game.CycleCount = 0;
            game.PendingGuess = null;
            game.ActiveGuesserId = null;
            game.Phase = GamePhase.Lobby;
            game.WinnerId = null;
            game.AutoAdvancePrompts = true;
        }
    }

    public bool AdvancePrompt(Game game)
    {
        lock (game.SyncRoot)
        {
            if (game.Prompts.Count == 0)
            {
                return false;
            }

            game.PromptIndex++;
            if (game.PromptIndex >= game.Prompts.Count)
            {
                game.PromptIndex = 0;
                game.CycleCount++;
            }

            return true;
        }
    }

    public bool ClaimGuess(Game game, string guesserId, string targetId, GuessOutcome outcome)
    {
        lock (game.SyncRoot)
        {
            if (game.Phase != GamePhase.Playing || game.ActiveGuesserId != guesserId || game.PendingGuess != null || guesserId == targetId)
            {
                return false;
            }

            var guesser = game.Players.FirstOrDefault(p => p.Id == guesserId);
            var target = game.Players.FirstOrDefault(p => p.Id == targetId);
            if (guesser == null || target == null)
            {
                return false;
            }

            if (outcome == GuessOutcome.Correct)
            {
                game.PendingGuess = new PendingGuess
                {
                    GuesserId = guesserId,
                    TargetId = targetId,
                    ClaimedOutcome = outcome,
                    Status = PendingGuessStatus.Pending
                };
                return true;
            }

            // Wrong guess: pass the turn immediately
            game.ActiveGuesserId = targetId;
            return true;
        }
    }

    public bool ConfirmPending(Game game, string targetId, bool confirm)
    {
        lock (game.SyncRoot)
        {
            if (game.Phase != GamePhase.Playing || game.PendingGuess == null || game.PendingGuess.TargetId != targetId)
            {
                return false;
            }

            var pending = game.PendingGuess;
            if (pending.Status != PendingGuessStatus.Pending)
            {
                return false;
            }

            if (confirm)
            {
                pending.Status = PendingGuessStatus.Confirmed;
                AbsorbEmpire(game, pending.GuesserId, pending.TargetId);
                CheckForWinner(game);
                if (game.AutoAdvancePrompts)
                {
                    AdvancePrompt(game);
                }
            }
            else
            {
                pending.Status = PendingGuessStatus.Denied;
                game.ActiveGuesserId = pending.TargetId;
            }

            game.PendingGuess = null;
            return true;
        }
    }

    public void SetAutoAdvance(Game game, bool enabled)
    {
        lock (game.SyncRoot)
        {
            game.AutoAdvancePrompts = enabled;
        }
    }

    public void NextTurn(Game game, string nextGuesserId)
    {
        lock (game.SyncRoot)
        {
            game.ActiveGuesserId = nextGuesserId;
        }
    }

    private void AbsorbEmpire(Game game, string guesserId, string targetId)
    {
        foreach (var player in game.Players)
        {
            if (player.Id == targetId || player.LeaderId == targetId)
            {
                player.LeaderId = guesserId;
            }
        }
    }

    private void CheckForWinner(Game game)
    {
        var leaders = game.Players.Where(p => p.LeaderId == null).ToList();
        if (leaders.Count == 1)
        {
            game.WinnerId = leaders[0].Id;
            game.Phase = GamePhase.Finished;
        }
    }

    private string GenerateCode()
    {
        string code;
        do
        {
            code = new string(Enumerable.Range(0, 6).Select(_ => CodeChars[_random.Next(CodeChars.Length)]).ToArray());
        } while (_games.ContainsKey(code));

        return code;
    }
}
