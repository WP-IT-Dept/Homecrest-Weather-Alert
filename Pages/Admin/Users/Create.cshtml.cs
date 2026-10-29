using System.ComponentModel.DataAnnotations;
using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomecrestWeatherAlert.Pages.Admin.Users;

public class CreateModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly PasswordResetEmailHelper _passwordEmails;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        UserManager<IdentityUser> userManager,
        PasswordResetEmailHelper passwordEmails,
        ILogger<CreateModel> logger)
    {
        _userManager = userManager;
        _passwordEmails = passwordEmails;
        _logger = logger;
    }

    public bool EmailConfigured => _passwordEmails.IsEmailConfigured;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Temporary password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Admin role")]
        public bool IsAdmin { get; set; }

        [Display(Name = "Email welcome + password reset link")]
        public bool SendWelcomeEmail { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new IdentityUser
        {
            UserName = Input.Email.Trim(),
            Email = Input.Email.Trim(),
            EmailConfirmed = true,
            LockoutEnabled = true
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        await _userManager.AddToRoleAsync(user, SeedData.UserRole);
        if (Input.IsAdmin)
        {
            await _userManager.AddToRoleAsync(user, SeedData.AdminRole);
        }

        var message = $"Created user {user.Email}.";
        if (Input.SendWelcomeEmail)
        {
            if (!_passwordEmails.IsEmailConfigured)
            {
                TempData["ErrorMessage"] =
                    message + " Welcome email was not sent — configure Exchange Online under Integrations.";
                return RedirectToPage("./Index");
            }

            try
            {
                await _passwordEmails.SendAccountCreatedAsync(user);
                message += " Welcome email sent.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send welcome email to {Email}.", user.Email);
                TempData["ErrorMessage"] = message + $" Welcome email failed: {ex.Message}";
                return RedirectToPage("./Index");
            }
        }

        TempData["StatusMessage"] = message;
        return RedirectToPage("./Index");
    }
}
