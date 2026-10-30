using System.ComponentModel.DataAnnotations;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomecrestWeatherAlert.Pages.Admin.Users;

public class ResetPasswordModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly PasswordResetEmailHelper _passwordEmails;
    private readonly ILogger<ResetPasswordModel> _logger;

    public ResetPasswordModel(
        UserManager<IdentityUser> userManager,
        PasswordResetEmailHelper passwordEmails,
        ILogger<ResetPasswordModel> logger)
    {
        _userManager = userManager;
        _passwordEmails = passwordEmails;
        _logger = logger;
    }

    public string Email { get; set; } = string.Empty;

    public bool EmailConfigured => _passwordEmails.IsEmailConfigured;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        Email = user.Email ?? user.UserName ?? id;
        Input.Id = user.Id;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.FindByIdAsync(Input.Id);
        if (user is null)
        {
            return NotFound();
        }

        Email = user.Email ?? user.UserName ?? Input.Id;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        TempData["StatusMessage"] = $"Password reset for {Email}.";
        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnPostEmailResetLinkAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        Email = user.Email ?? user.UserName ?? id;
        Input.Id = user.Id;

        if (!_passwordEmails.IsEmailConfigured)
        {
            ModelState.AddModelError(string.Empty, "Exchange Online email is not configured. Open Admin → Integrations.");
            return Page();
        }

        try
        {
            await _passwordEmails.SendPasswordResetLinkAsync(user);
            TempData["StatusMessage"] = $"Password reset link emailed to {Email}.";
            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to email password reset link to {Email}.", Email);
            ModelState.AddModelError(string.Empty, $"Email failed: {ex.Message}");
            return Page();
        }
    }
}
