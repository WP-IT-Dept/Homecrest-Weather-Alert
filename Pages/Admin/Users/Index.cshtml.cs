using HomecrestWeatherAlert.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Pages.Admin.Users;

public class IndexModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _configuration;

    public IndexModel(UserManager<IdentityUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    public IList<UserRow> Users { get; set; } = new List<UserRow>();

    public class UserRow
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Disabled { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsProtectedSeedAdmin { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }

    public async Task OnGetAsync()
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
        Users = new List<UserRow>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            Users.Add(new UserRow
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? user.Id,
                Disabled = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                IsAdmin = roles.Contains(SeedData.AdminRole),
                IsProtectedSeedAdmin = SeedData.IsProtectedSeedAdmin(user, _configuration),
                Roles = roles
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

        if (SeedData.IsProtectedSeedAdmin(user, _configuration))
        {
            TempData["ErrorMessage"] = $"Cannot disable the protected seed admin ({SeedData.DefaultAdminEmail}).";
            return RedirectToPage();
        }

        if (IsSelf(user))
        {
            TempData["ErrorMessage"] = "Cannot disable your own account from this screen.";
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

    public async Task<IActionResult> OnPostGrantAdminAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (!await _userManager.IsInRoleAsync(user, SeedData.UserRole))
        {
            await _userManager.AddToRoleAsync(user, SeedData.UserRole);
        }

        if (!await _userManager.IsInRoleAsync(user, SeedData.AdminRole))
        {
            await _userManager.AddToRoleAsync(user, SeedData.AdminRole);
        }

        TempData["StatusMessage"] = $"Granted Admin role to {user.Email}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAdminAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (SeedData.IsProtectedSeedAdmin(user, _configuration))
        {
            TempData["ErrorMessage"] =
                $"Cannot remove the Admin role from the protected seed admin ({SeedData.DefaultAdminEmail}).";
            return RedirectToPage();
        }

        if (IsSelf(user))
        {
            TempData["ErrorMessage"] = "Cannot remove your own Admin role from this screen.";
            return RedirectToPage();
        }

        if (await _userManager.IsInRoleAsync(user, SeedData.AdminRole))
        {
            await _userManager.RemoveFromRoleAsync(user, SeedData.AdminRole);
        }

        if (!await _userManager.IsInRoleAsync(user, SeedData.UserRole))
        {
            await _userManager.AddToRoleAsync(user, SeedData.UserRole);
        }

        TempData["StatusMessage"] = $"Removed Admin role from {user.Email}. They remain a User.";
        return RedirectToPage();
    }

    private bool IsSelf(IdentityUser user) =>
        string.Equals(user.Email, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(user.UserName, User.Identity?.Name, StringComparison.OrdinalIgnoreCase);
}
