using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Pages.Employees;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public IList<Employee> Employees { get; set; } = new List<Employee>();

    public async Task OnGetAsync()
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
