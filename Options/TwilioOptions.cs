namespace HomecrestWeatherAlert.Options;

public class TwilioOptions
{
    public const string SectionName = "Twilio";

    /// <summary>Account SID (starts with AC...).</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>API Key SID (starts with SK...). Preferred over AuthToken.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>API Key secret — prefer env/User Secrets, never commit real values.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// Account Auth Token. Used to validate inbound webhook signatures (RequestValidator).
    /// Also a temporary send fallback when ApiKey/ApiSecret are not set.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>E.164 From number, e.g. +15551234567 (trial number for now).</summary>
    public string FromNumber { get; set; } = string.Empty;

    public bool HasApiKeyCredentials =>
        IsReal(AccountSid)
        && IsReal(ApiKey)
        && IsReal(ApiSecret)
        && IsReal(FromNumber);

    public bool HasAuthTokenCredentials =>
        IsReal(AccountSid)
        && IsReal(AuthToken)
        && IsReal(FromNumber);

    /// <summary>True when API key (preferred) or legacy Auth Token credentials are present.</summary>
    public bool IsConfigured => HasApiKeyCredentials || HasAuthTokenCredentials;

    public bool PreferApiKey => HasApiKeyCredentials;

    private static bool IsReal(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
}
