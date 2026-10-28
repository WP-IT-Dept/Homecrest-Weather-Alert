using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Pages.Employees;

public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DeleteModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public Employee? Employee { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        return Employee is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (employee is null)
        {
            return NotFound();
        }

        _db.Employees.Remove(employee);
        await _db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Deleted {employee.Name}.";
        return RedirectToPage("./Index");
    }
}
