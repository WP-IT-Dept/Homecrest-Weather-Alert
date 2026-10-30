using System.Text;
using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Services;

public record EmployeeCsvImportResult(
    int Created,
    int SkippedDuplicate,
    int SkippedInvalid,
    IReadOnlyList<string> Errors);

/// <summary>Parses and imports employee CSV rows (Name, Phone, optional Active).</summary>
public class EmployeeCsvImporter
{
    public const string TemplateContents =
        "Name,Phone,Active\r\n" +
        "Jane Doe,555-123-4567,true\r\n" +
        "John Smith,(555) 987-6543,false\r\n";

    private readonly ApplicationDbContext _db;

    public EmployeeCsvImporter(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<EmployeeCsvImportResult> ImportAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return await ImportTextAsync(text, cancellationToken);
    }

    public async Task<EmployeeCsvImportResult> ImportTextAsync(
        string csvText,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var created = 0;
        var skippedDup = 0;
        var skippedInvalid = 0;

        var lines = SplitLines(csvText);
        if (lines.Count == 0)
        {
            errors.Add("The CSV file is empty.");
            return new EmployeeCsvImportResult(0, 0, 0, errors);
        }

        var startIndex = 0;
        var header = ParseCsvLine(lines[0]);
        if (LooksLikeHeader(header))
        {
            startIndex = 1;
            if (!HeaderHasNameAndPhone(header))
            {
                errors.Add("Header must include Name and Phone columns (optional Active).");
                return new EmployeeCsvImportResult(0, 0, 0, errors);
            }
        }

        var existingE164 = (await _db.Employees
                .AsNoTracking()
                .Where(e => e.PhoneE164 != null)
                .Select(e => e.PhoneE164!)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var seenInFile = new HashSet<string>(StringComparer.Ordinal);
        var toAdd = new List<Employee>();
        var now = DateTimeOffset.UtcNow;

        for (var i = startIndex; i < lines.Count; i++)
        {
            var rowNumber = i + 1;
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cols = ParseCsvLine(line);
            if (cols.Count == 0 || cols.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string name;
            string phone;
            string? activeRaw = null;

            if (startIndex == 1 && HeaderHasNameAndPhone(header))
            {
                name = GetColumn(cols, header, "name") ?? string.Empty;
                phone = GetColumn(cols, header, "phone") ?? string.Empty;
                activeRaw = GetColumn(cols, header, "active");
            }
            else
            {
                // Positional: Name, Phone, [Active]
                name = cols.ElementAtOrDefault(0) ?? string.Empty;
                phone = cols.ElementAtOrDefault(1) ?? string.Empty;
                activeRaw = cols.ElementAtOrDefault(2);
            }

            name = name.Trim();
            phone = phone.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                skippedInvalid++;
                errors.Add($"Row {rowNumber}: Name is required.");
                continue;
            }

            if (name.Length > 200)
            {
                skippedInvalid++;
                errors.Add($"Row {rowNumber}: Name must be 200 characters or fewer.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                skippedInvalid++;
                errors.Add($"Row {rowNumber}: Phone is required.");
                continue;
            }

            var e164 = PhoneNormalizer.ToE164(phone);
            if (e164 is null)
            {
                skippedInvalid++;
                errors.Add($"Row {rowNumber}: Invalid phone '{phone}' (use a US 10-digit or +1… number).");
                continue;
            }

            if (!TryParseActive(activeRaw, out var isActive, out var activeError))
            {
                skippedInvalid++;
                errors.Add($"Row {rowNumber}: {activeError}");
                continue;
            }

            if (!seenInFile.Add(e164))
            {
                skippedDup++;
                errors.Add($"Row {rowNumber}: Duplicate phone in file ({e164}); skipped.");
                continue;
            }

            if (existingE164.Contains(e164))
            {
                skippedDup++;
                errors.Add($"Row {rowNumber}: Phone {e164} already exists; skipped.");
                continue;
            }

            toAdd.Add(new Employee
            {
                Name = name,
                Phone = phone,
                PhoneE164 = e164,
                IsActive = isActive,
                CreatedAt = now,
                UpdatedAt = now
            });
            existingE164.Add(e164);
        }

        if (toAdd.Count > 0)
        {
            _db.Employees.AddRange(toAdd);
            await _db.SaveChangesAsync(cancellationToken);
            created = toAdd.Count;
        }

        return new EmployeeCsvImportResult(created, skippedDup, skippedInvalid, errors);
    }

    internal static bool TryParseActive(string? raw, out bool isActive, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            isActive = true;
            return true;
        }

        var v = raw.Trim().ToLowerInvariant();
        switch (v)
        {
            case "true":
            case "yes":
            case "y":
            case "1":
            case "active":
                isActive = true;
                return true;
            case "false":
            case "no":
            case "n":
            case "0":
            case "inactive":
                isActive = false;
                return true;
            default:
                isActive = true;
                error = $"Active value '{raw}' is invalid (use true/false, yes/no, 1/0, or leave blank).";
                return false;
        }
    }

    private static bool LooksLikeHeader(IReadOnlyList<string> cols) =>
        cols.Any(c => string.Equals(c.Trim(), "Name", StringComparison.OrdinalIgnoreCase))
        || cols.Any(c => string.Equals(c.Trim(), "Phone", StringComparison.OrdinalIgnoreCase));

    private static bool HeaderHasNameAndPhone(IReadOnlyList<string> header)
    {
        var names = header.Select(h => h.Trim().ToLowerInvariant()).ToHashSet();
        return names.Contains("name") && names.Contains("phone");
    }

    private static string? GetColumn(IReadOnlyList<string> cols, IReadOnlyList<string> header, string key)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i].Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return i < cols.Count ? cols[i] : null;
            }
        }

        return null;
    }

    private static List<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.None)
            .ToList();

    /// <summary>Minimal RFC4180-ish CSV line parser (quoted fields, commas).</summary>
    internal static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString());
        return result;
    }
}
