namespace HomecrestWeatherAlert.Options;

/// <summary>
/// Admin-editable integration values persisted via ASP.NET Data Protection
/// (encrypted file under App_Data). Non-empty fields override appsettings/env.
/// </summary>
public class IntegrationsOverlay
{
    public string? ExchangeTenantId { get; set; }
    public string? ExchangeClientId { get; set; }
    public string? ExchangeClientSecret { get; set; }
    public string? ExchangeSenderAddress { get; set; }

    public string? TwilioAccountSid { get; set; }
    public string? TwilioApiKey { get; set; }
    public string? TwilioApiSecret { get; set; }
    public string? TwilioAuthToken { get; set; }
    public string? TwilioFromNumber { get; set; }
}
