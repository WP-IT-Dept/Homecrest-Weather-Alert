namespace HomecrestWeatherAlert.Options;

/// <summary>
/// Microsoft Graph / Exchange Online app-only mail settings (client credentials).
/// Sends as <see cref="SenderAddress"/> via <c>POST /users/{sender}/sendMail</c>.
/// </summary>
public class ExchangeOnlineOptions
{
    public const string SectionName = "ExchangeOnline";

    /// <summary>Entra ID (Azure AD) directory (tenant) ID.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>App registration application (client) ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>App registration client secret — prefer env/User Secrets, never commit real values.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Mailbox UPN or SMTP address used as the From account (e.g. noreply@homecrest.com).
    /// Must be a real Exchange Online mailbox the app is allowed to send as.
    /// </summary>
    public string SenderAddress { get; set; } = string.Empty;

    public bool IsConfigured =>
        IsReal(TenantId)
        && IsReal(ClientId)
        && IsReal(ClientSecret)
        && IsReal(SenderAddress);

    private static bool IsReal(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
}
