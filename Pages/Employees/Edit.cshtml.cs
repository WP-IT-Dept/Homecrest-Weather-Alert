using System.ComponentModel.DataAnnotations;
using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Pages.Employees;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public EditModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(32)]
        [Display(Name = "Phone")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Active (uncheck only for leavers / HR removal)")]
        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (employee is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = employee.Id,
            Name = employee.Name,
            Phone = employee.Phone,
            IsActive = employee.IsActive
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == Input.Id);
        if (employee is null)
        {
            return NotFound();
        }

        var e164 = PhoneNormalizer.ToE164(Input.Phone);
        if (e164 is null)
        {
            ModelState.AddModelError(nameof(Input.Phone), "Enter a valid US phone number (10 digits, or +1…).");
            return Page();
        }

        employee.Name = Input.Name.Trim();
        employee.Phone = Input.Phone.Trim();
        employee.PhoneE164 = e164;
        employee.IsActive = Input.IsActive;
        employee.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Updated {employee.Name}.";
        return RedirectToPage("./Index");
    }
}
