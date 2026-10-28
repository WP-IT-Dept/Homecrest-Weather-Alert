using System.ComponentModel.DataAnnotations;
using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    public List<SelectListItem> ActiveOptions { get; } = EmployeeActiveOptions.Items;

    public DateTimeOffset? OptedOutAt { get; set; }

    public class InputModel
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(32)]
        [Display(Name = "Phone")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "Status")]
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
        OptedOutAt = employee.OptedOutAt;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.IsActive = CreateModel.ReadIsActive(Request.Form, Input.IsActive);

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
            ModelState.AddModelError("Input.Phone", "Enter a valid US phone number (10 digits, or +1…).");
            return Page();
        }

        employee.Name = Input.Name.Trim();
        employee.Phone = Input.Phone.Trim();
        employee.PhoneE164 = e164;
        employee.IsActive = Input.IsActive;
        if (Input.IsActive)
        {
            // Explicit reactivation by staff clears SMS opt-out marker.
            employee.OptedOutAt = null;
        }
        else if (employee.OptedOutAt is null)
        {
            // Manual deactivate without a prior STOP — leave OptedOutAt null (HR/leaver).
        }

        employee.UpdatedAt = DateTimeOffset.UtcNow;
        OptedOutAt = employee.OptedOutAt;

        await _db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Updated {employee.Name}.";
        return RedirectToPage("./Index");
    }
}
