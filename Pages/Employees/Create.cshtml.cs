using System.ComponentModel.DataAnnotations;
using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

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

    public List<SelectListItem> ActiveOptions { get; } = EmployeeActiveOptions.Items;

    public class InputModel
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(32)]
        [Display(Name = "Phone")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Status")]
        public bool IsActive { get; set; } = true;
    }

    public void OnGet()
    {
        Input.IsActive = true;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Prefer the single select value; ignore any duplicate checkbox leftovers.
        Input.IsActive = ReadIsActive(Request.Form, Input.IsActive);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var e164 = PhoneNormalizer.ToE164(Input.Phone);
        if (e164 is null)
        {
            ModelState.AddModelError("Input.Phone", "Enter a valid US phone number (10 digits, or +1…).");
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

    /// <summary>
    /// Resolve Active from the form. A single select posts one value. Legacy checkbox+hidden
    /// may post both true and false in either order — treat as active if any value is true,
    /// inactive when only false is posted (unchecked).
    /// </summary>
    internal static bool ReadIsActive(IFormCollection form, bool modelBoundFallback)
    {
        var values = form["Input.IsActive"];
        if (values.Count == 0)
        {
            return modelBoundFallback;
        }

        var sawFalse = false;
        foreach (var raw in values)
        {
            if (!bool.TryParse(raw, out var parsed))
            {
                continue;
            }

            if (parsed)
            {
                return true;
            }

            sawFalse = true;
        }

        return sawFalse ? false : modelBoundFallback;
    }
}

