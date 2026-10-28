using System.Text.RegularExpressions;

namespace HomecrestWeatherAlert.Services;

public static partial class PhoneNormalizer
{
    /// <summary>
    /// Normalize a US phone number to E.164 (+1XXXXXXXXXX) when possible.
    /// Returns null if the input cannot be confidently normalized.
    /// </summary>
    public static string? ToE164(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();

        // Already E.164 US
        if (UsE164Regex().IsMatch(trimmed))
        {
            return trimmed;
        }

        var digits = DigitsOnlyRegex().Replace(trimmed, string.Empty);

        if (digits.Length == 11 && digits.StartsWith('1'))
        {
            return "+" + digits;
        }

        if (digits.Length == 10)
        {
            return "+1" + digits;
        }

        // International-looking E.164 (non-US) — pass through if already +digits
        if (trimmed.StartsWith('+') && digits.Length is >= 8 and <= 15)
        {
            return "+" + digits;
        }

        return null;
    }

    public static bool TryNormalize(string? raw, out string e164)
    {
        var result = ToE164(raw);
        e164 = result ?? string.Empty;
        return result is not null;
    }

    /// <summary>Mask a phone for UI so other recipients' full numbers stay private in send logs.</summary>
    public static string Mask(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "****";
        }

        var digits = DigitsOnlyRegex().Replace(phone, string.Empty);
        if (digits.Length < 4)
        {
            return "****";
        }

        return "***-***-" + digits[^4..];
    }

    [GeneratedRegex(@"^\+1\d{10}$")]
    private static partial Regex UsE164Regex();

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnlyRegex();
}
