namespace HomecrestWeatherAlert.Options;

public class TwilioOptions
{
    public const string SectionName = "Twilio";

    /// <summary>Account SID (starts with AC...).</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>Auth Token — prefer env/User Secrets, never commit real values.</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>E.164 From number, e.g. +15551234567 (trial number for now).</summary>
    public string FromNumber { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid)
        && !AccountSid.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(AuthToken)
        && !AuthToken.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(FromNumber)
        && !FromNumber.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
}
