using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using HomecrestWeatherAlert.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomecrestWeatherAlert.Tests;

public class EmployeeOptOutServiceTests
{
    [Fact]
    public async Task ApplyOptOut_sets_inactive_and_OptedOutAt()
    {
        await using var db = CreateDb();
        db.Employees.Add(new Employee
        {
            Name = "Pat",
            Phone = "(555) 222-3333",
            PhoneE164 = "+15552223333",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new EmployeeOptOutService(db, NullLogger<EmployeeOptOutService>.Instance);
        var id = await service.ApplyOptOutAsync("+1 555 222 3333");

        Assert.NotNull(id);
        var employee = await db.Employees.SingleAsync();
        Assert.False(employee.IsActive);
        Assert.NotNull(employee.OptedOutAt);
        Assert.True(employee.OptedOutAt <= DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task ApplyOptOut_returns_null_when_no_match()
    {
        await using var db = CreateDb();
        var service = new EmployeeOptOutService(db, NullLogger<EmployeeOptOutService>.Instance);
        var id = await service.ApplyOptOutAsync("+15559998888");
        Assert.Null(id);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"DataSource=optout-{Guid.NewGuid():N}.db")
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
