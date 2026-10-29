namespace HomecrestWeatherAlert.Services;

public static class SmsOptOutKeywords
{
    /// <summary>
    /// Common US SMS opt-out keywords (Twilio / carrier STOP filtering).
    /// Matched as the whole message body (trimmed), case-insensitive.
    /// </summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "STOP",
        "STOPALL",
        "UNSUBSCRIBE",
        "CANCEL",
        "END",
        "QUIT"
    };

    public static bool IsOptOut(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var trimmed = body.Trim();
        // Allow a trailing period or exclamation ("STOP!" / "STOP.")
        trimmed = trimmed.TrimEnd('.', '!', '?');
        return Keywords.Contains(trimmed);
    }
}
