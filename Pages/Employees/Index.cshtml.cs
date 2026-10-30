using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace HomecrestWeatherAlert.Pages.Employees;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly EmployeeCsvImporter _csvImporter;

    public IndexModel(ApplicationDbContext db, EmployeeCsvImporter csvImporter)
    {
        _db = db;
        _csvImporter = csvImporter;
    }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty]
    public List<int> SelectedIds { get; set; } = [];

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    public IList<Employee> Employees { get; set; } = new List<Employee>();

    public async Task OnGetAsync()
    {
        await LoadEmployeesAsync();
    }

    public IActionResult OnGetTemplate()
    {
        var bytes = Encoding.UTF8.GetBytes(EmployeeCsvImporter.TemplateContents);
        return File(bytes, "text/csv", "employees-template.csv");
    }

    public async Task<IActionResult> OnPostImportAsync(CancellationToken cancellationToken)
    {
        if (CsvFile is null || CsvFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Choose a CSV file to upload.";
            return RedirectToPage(new { q = Q });
        }

        if (CsvFile.Length > 2 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "CSV file must be 2 MB or smaller.";
            return RedirectToPage(new { q = Q });
        }

        await using var stream = CsvFile.OpenReadStream();
        var result = await _csvImporter.ImportAsync(stream, cancellationToken);

        var summary =
            $"Import finished: {result.Created} added, {result.SkippedDuplicate} duplicate(s) skipped, {result.SkippedInvalid} invalid row(s).";
        TempData["StatusMessage"] = summary;

        if (result.Errors.Count > 0)
        {
            // Cap display so the alert stays readable.
            var shown = result.Errors.Take(25).ToList();
            if (result.Errors.Count > shown.Count)
            {
                shown.Add($"…and {result.Errors.Count - shown.Count} more.");
            }

            TempData["ImportErrors"] = string.Join("\n", shown);
        }

        return RedirectToPage(new { q = Q });
    }

    public async Task<IActionResult> OnPostBulkActivateAsync()
    {
        var count = await SetActiveAsync(SelectedIds, active: true);
        TempData["StatusMessage"] = count == 0
            ? "No employees selected."
            : $"Marked {count} employee(s) active.";
        return RedirectToPage(new { q = Q });
    }

    public async Task<IActionResult> OnPostBulkDeactivateAsync()
    {
        var count = await SetActiveAsync(SelectedIds, active: false);
        TempData["StatusMessage"] = count == 0
            ? "No employees selected."
            : $"Marked {count} employee(s) inactive.";
        return RedirectToPage(new { q = Q });
    }

    public async Task<IActionResult> OnPostBulkDeleteAsync()
    {
        if (SelectedIds.Count == 0)
        {
            TempData["ErrorMessage"] = "No employees selected.";
            return RedirectToPage(new { q = Q });
        }

        var employees = await _db.Employees
            .Where(e => SelectedIds.Contains(e.Id))
            .ToListAsync();

        if (employees.Count == 0)
        {
            TempData["ErrorMessage"] = "No matching employees to delete.";
            return RedirectToPage(new { q = Q });
        }

        _db.Employees.RemoveRange(employees);
        await _db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Deleted {employees.Count} employee(s).";
        return RedirectToPage(new { q = Q });
    }

    private async Task<int> SetActiveAsync(IReadOnlyCollection<int> ids, bool active)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        var employees = await _db.Employees
            .Where(e => ids.Contains(e.Id))
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        foreach (var employee in employees)
        {
            employee.IsActive = active;
            if (active)
            {
                // Clearing opt-out when reactivating is intentional for bulk restore by staff.
                employee.OptedOutAt = null;
            }

            employee.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        return employees.Count;
    }

    private async Task LoadEmployeesAsync()
    {
        var query = _db.Employees.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim();
            query = query.Where(e =>
                e.Name.Contains(term)
                || e.Phone.Contains(term)
                || (e.PhoneE164 != null && e.PhoneE164.Contains(term)));
        }

        Employees = await query
            .OrderByDescending(e => e.IsActive)
            .ThenBy(e => e.Name)
            .ToListAsync();
    }
}
