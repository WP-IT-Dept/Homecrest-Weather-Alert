using HomecrestWeatherAlert.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Pages.Admin.Users;

public class IndexModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;

    public IndexModel(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public IList<UserRow> Users { get; set; } = new List<UserRow>();

    public class UserRow
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Disabled { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }

    public async Task OnGetAsync()
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
        Users = new List<UserRow>();
        foreach (var user in users)
        {
            Users.Add(new UserRow
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? user.Id,
                Disabled = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                Roles = await _userManager.GetRolesAsync(user)
            });
        }
    }

    public async Task<IActionResult> OnPostDisableAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (string.Equals(user.Email, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.UserName, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            TempData["StatusMessage"] = "Cannot disable your own account from this screen.";
            return RedirectToPage();
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(user);
        TempData["StatusMessage"] = $"Disabled {user.Email}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEnableAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.LockoutEnd = null;
        await _userManager.UpdateAsync(user);
        TempData["StatusMessage"] = $"Enabled {user.Email}.";
        return RedirectToPage();
    }
}
