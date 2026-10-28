using System.ComponentModel.DataAnnotations;
using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomecrestWeatherAlert.Pages.Employees;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public CreateModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(32)]
        [Display(Name = "Phone")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Active (uncheck only for leavers / HR removal)")]
        public bool IsActive { get; set; } = true;
    }

    public void OnGet()
    {
        Input.IsActive = true;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var e164 = PhoneNormalizer.ToE164(Input.Phone);
        if (e164 is null)
        {
            ModelState.AddModelError(nameof(Input.Phone), "Enter a valid US phone number (10 digits, or +1…).");
            return Page();
        }

        var employee = new Employee
        {
            Name = Input.Name.Trim(),
            Phone = Input.Phone.Trim(),
            PhoneE164 = e164,
            IsActive = Input.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = $"Added {employee.Name}.";
        return RedirectToPage("./Index");
    }
}
