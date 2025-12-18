using EmpireWebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EmpireWebApp.Pages.Game;

public class TvModel : PageModel
{
    private readonly GameStore _store;

    public TvModel(GameStore store)
    {
        _store = store;
    }

    [BindProperty(SupportsGet = true, Name = "code")]
    public string Code { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        Code = Code.ToUpperInvariant();
        if (!_store.TryGetGame(Code.ToUpperInvariant(), out var _))
        {
            return RedirectToPage("/Join");
        }

        return Page();
    }
}
