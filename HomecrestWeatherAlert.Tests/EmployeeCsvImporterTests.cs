using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using HomecrestWeatherAlert.Services;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Tests;

public class EmployeeCsvImporterTests
{
    [Fact]
    public async Task Import_creates_rows_and_normalizes_phones()
    {
        await using var db = CreateDb();
        var importer = new EmployeeCsvImporter(db);

        var csv =
            "Name,Phone,Active\n" +
            "Pat Lee,5551112222,true\n" +
            "Sam Ray,(555) 333-4444,\n" +
            "Inactive Person,+15555556666,false\n";

        var result = await importer.ImportTextAsync(csv);

        Assert.Equal(3, result.Created);
        Assert.Equal(0, result.SkippedDuplicate);
        Assert.Equal(0, result.SkippedInvalid);
        Assert.Empty(result.Errors);

        var employees = await db.Employees.OrderBy(e => e.Name).ToListAsync();
        Assert.Equal(3, employees.Count);
        Assert.Contains(employees, e => e.Name == "Pat Lee" && e.PhoneE164 == "+15551112222" && e.IsActive);
        Assert.Contains(employees, e => e.Name == "Sam Ray" && e.PhoneE164 == "+15553334444" && e.IsActive);
        Assert.Contains(employees, e => e.Name == "Inactive Person" && !e.IsActive);
    }

    [Fact]
    public async Task Import_skips_duplicates_and_reports_invalid_rows()
    {
        await using var db = CreateDb();
        db.Employees.Add(new Employee
        {
            Name = "Existing",
            Phone = "5550001111",
            PhoneE164 = "+15550001111",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var importer = new EmployeeCsvImporter(db);
        var csv =
            "Name,Phone,Active\n" +
            "Dup Existing,555-000-1111,true\n" +
            "Bad Phone,not-a-phone,true\n" +
            "File Dup A,5559998888,yes\n" +
            "File Dup B,5559998888,yes\n" +
            "Good One,5552223333,1\n";

        var result = await importer.ImportTextAsync(csv);

        Assert.Equal(2, result.Created); // File Dup A + Good One
        Assert.Equal(2, result.SkippedDuplicate); // existing + second file dup
        Assert.Equal(1, result.SkippedInvalid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid phone", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, await db.Employees.CountAsync());
    }

    [Fact]
    public void TryParseActive_defaults_blank_to_true()
    {
        Assert.True(EmployeeCsvImporter.TryParseActive(null, out var a, out var err));
        Assert.True(a);
        Assert.Null(err);

        Assert.False(EmployeeCsvImporter.TryParseActive("maybe", out _, out err));
        Assert.NotNull(err);
    }

    [Fact]
    public void ParseCsvLine_handles_quotes()
    {
        var cols = EmployeeCsvImporter.ParseCsvLine("\"Doe, Jane\",5551234567,true");
        Assert.Equal(["Doe, Jane", "5551234567", "true"], cols);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"DataSource=emp-csv-{Guid.NewGuid():N}.db")
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
