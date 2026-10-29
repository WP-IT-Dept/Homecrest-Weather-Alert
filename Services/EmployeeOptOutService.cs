using HomecrestWeatherAlert.Data;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Services;

public class EmployeeOptOutService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<EmployeeOptOutService> _logger;

    public EmployeeOptOutService(ApplicationDbContext db, ILogger<EmployeeOptOutService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Mark the employee matching <paramref name="fromPhone"/> inactive and set OptedOutAt (UTC).
    /// Returns the employee id when a match was updated; null when no match.
    /// </summary>
    public async Task<int?> ApplyOptOutAsync(string? fromPhone, CancellationToken cancellationToken = default)
    {
        var e164 = PhoneNormalizer.ToE164(fromPhone);
        if (e164 is null)
        {
            _logger.LogWarning("Inbound opt-out From number could not be normalized: {From}", PhoneNormalizer.Mask(fromPhone));
            return null;
        }

        var employees = await _db.Employees.ToListAsync(cancellationToken);
        var match = employees.FirstOrDefault(e =>
            string.Equals(e.PhoneE164, e164, StringComparison.OrdinalIgnoreCase)
            || string.Equals(PhoneNormalizer.ToE164(e.Phone), e164, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            _logger.LogInformation(
                "Inbound STOP from {Masked} did not match any employee.",
                PhoneNormalizer.Mask(e164));
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        match.IsActive = false;
        match.OptedOutAt ??= now; // keep first opt-out timestamp
        match.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee {EmployeeId} opted out via SMS at {OptedOutAt:o}.",
            match.Id,
            match.OptedOutAt);

        return match.Id;
    }
}
