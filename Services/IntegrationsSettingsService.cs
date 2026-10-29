using System.Text.Json;
using HomecrestWeatherAlert.Options;
using Microsoft.AspNetCore.DataProtection;

namespace HomecrestWeatherAlert.Services;

/// <summary>
/// Merges appsettings/environment with an encrypted App_Data overlay so admins can
/// rotate Graph/Twilio secrets from the UI without redeploying.
/// </summary>
public class IntegrationsSettingsService
{
    public const string ProtectorPurpose = "HomecrestWeatherAlert.Integrations.v1";
    public const string RelativeStorePath = "App_Data/integrations.protected";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConfiguration _configuration;
    private readonly IDataProtector _protector;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<IntegrationsSettingsService> _logger;
    private readonly object _gate = new();
    private IntegrationsOverlay _overlay = new();
    private int _version;

    public IntegrationsSettingsService(
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        IHostEnvironment environment,
        ILogger<IntegrationsSettingsService> logger)
    {
        _configuration = configuration;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _environment = environment;
        _logger = logger;
        ReloadFromDisk();
    }

    /// <summary>Increments whenever the overlay is saved (Graph client cache invalidation).</summary>
    public int Version
    {
        get { lock (_gate) return _version; }
    }

    public string StorePath =>
        Path.Combine(_environment.ContentRootPath, RelativeStorePath);

    public ExchangeOnlineOptions GetExchangeOnline()
    {
        lock (_gate)
        {
            var baseline = new ExchangeOnlineOptions();
            _configuration.GetSection(ExchangeOnlineOptions.SectionName).Bind(baseline);
            return MergeExchange(baseline, _overlay);
        }
    }

    public TwilioOptions GetTwilio()
    {
        lock (_gate)
        {
            var baseline = new TwilioOptions();
            _configuration.GetSection(TwilioOptions.SectionName).Bind(baseline);
            return MergeTwilio(baseline, _overlay);
        }
    }

    /// <summary>Snapshot of overlay flags for the admin UI (never includes secret plaintext).</summary>
    public IntegrationsPublicStatus GetPublicStatus()
    {
        lock (_gate)
        {
            var exchange = MergeExchange(BindExchangeBaseline(), _overlay);
            var twilio = MergeTwilio(BindTwilioBaseline(), _overlay);
            return new IntegrationsPublicStatus
            {
                ExchangeTenantId = exchange.TenantId,
                ExchangeClientId = exchange.ClientId,
                ExchangeSenderAddress = exchange.SenderAddress,
                ExchangeClientSecretConfigured = !string.IsNullOrWhiteSpace(exchange.ClientSecret)
                    && !exchange.ClientSecret.Contains("YOUR_", StringComparison.OrdinalIgnoreCase),
                ExchangeConfigured = exchange.IsConfigured,
                TwilioAccountSid = twilio.AccountSid,
                TwilioApiKey = twilio.ApiKey,
                TwilioFromNumber = twilio.FromNumber,
                TwilioApiSecretConfigured = !string.IsNullOrWhiteSpace(twilio.ApiSecret)
                    && !twilio.ApiSecret.Contains("YOUR_", StringComparison.OrdinalIgnoreCase),
                TwilioAuthTokenConfigured = !string.IsNullOrWhiteSpace(twilio.AuthToken)
                    && !twilio.AuthToken.Contains("YOUR_", StringComparison.OrdinalIgnoreCase),
                TwilioConfigured = twilio.IsConfigured,
                OverlayFileExists = File.Exists(StorePath)
            };
        }
    }

    public void SaveExchangeOnline(
        string? tenantId,
        string? clientId,
        string? clientSecret,
        string? senderAddress,
        bool clearClientSecret)
    {
        lock (_gate)
        {
            ApplyIfProvided(v => _overlay.ExchangeTenantId = v, tenantId);
            ApplyIfProvided(v => _overlay.ExchangeClientId = v, clientId);
            ApplyIfProvided(v => _overlay.ExchangeSenderAddress = v, senderAddress);

            if (clearClientSecret)
            {
                _overlay.ExchangeClientSecret = null;
            }
            else if (!string.IsNullOrWhiteSpace(clientSecret) && !IsMask(clientSecret))
            {
                _overlay.ExchangeClientSecret = clientSecret.Trim();
            }

            PersistLocked();
        }
    }

    public void SaveTwilio(
        string? accountSid,
        string? apiKey,
        string? apiSecret,
        string? authToken,
        string? fromNumber,
        bool clearApiSecret,
        bool clearAuthToken)
    {
        lock (_gate)
        {
            ApplyIfProvided(v => _overlay.TwilioAccountSid = v, accountSid);
            ApplyIfProvided(v => _overlay.TwilioApiKey = v, apiKey);
            ApplyIfProvided(v => _overlay.TwilioFromNumber = v, fromNumber);

            if (clearApiSecret)
            {
                _overlay.TwilioApiSecret = null;
            }
            else if (!string.IsNullOrWhiteSpace(apiSecret) && !IsMask(apiSecret))
            {
                _overlay.TwilioApiSecret = apiSecret.Trim();
            }

            if (clearAuthToken)
            {
                _overlay.TwilioAuthToken = null;
            }
            else if (!string.IsNullOrWhiteSpace(authToken) && !IsMask(authToken))
            {
                _overlay.TwilioAuthToken = authToken.Trim();
            }

            PersistLocked();
        }
    }

    public void ReloadFromDisk()
    {
        lock (_gate)
        {
            var path = StorePath;
            if (!File.Exists(path))
            {
                _overlay = new IntegrationsOverlay();
                return;
            }

            try
            {
                var protectedPayload = File.ReadAllText(path);
                var json = _protector.Unprotect(protectedPayload);
                _overlay = JsonSerializer.Deserialize<IntegrationsOverlay>(json, JsonOptions)
                    ?? new IntegrationsOverlay();
                _version++;
                _logger.LogInformation("Loaded encrypted integrations overlay from {Path}.", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load integrations overlay from {Path}. Using configuration only.", path);
                _overlay = new IntegrationsOverlay();
            }
        }
    }

    private void PersistLocked()
    {
        var path = StorePath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(_overlay, JsonOptions);
        var protectedPayload = _protector.Protect(json);
        File.WriteAllText(path, protectedPayload);
        _version++;
        _logger.LogInformation("Saved encrypted integrations overlay to {Path}.", path);
    }

    private ExchangeOnlineOptions BindExchangeBaseline()
    {
        var baseline = new ExchangeOnlineOptions();
        _configuration.GetSection(ExchangeOnlineOptions.SectionName).Bind(baseline);
        return baseline;
    }

    private TwilioOptions BindTwilioBaseline()
    {
        var baseline = new TwilioOptions();
        _configuration.GetSection(TwilioOptions.SectionName).Bind(baseline);
        return baseline;
    }

    private static ExchangeOnlineOptions MergeExchange(ExchangeOnlineOptions baseline, IntegrationsOverlay overlay)
    {
        return new ExchangeOnlineOptions
        {
            TenantId = FirstReal(overlay.ExchangeTenantId, baseline.TenantId),
            ClientId = FirstReal(overlay.ExchangeClientId, baseline.ClientId),
            ClientSecret = FirstReal(overlay.ExchangeClientSecret, baseline.ClientSecret),
            SenderAddress = FirstReal(overlay.ExchangeSenderAddress, baseline.SenderAddress)
        };
    }

    private static TwilioOptions MergeTwilio(TwilioOptions baseline, IntegrationsOverlay overlay)
    {
        return new TwilioOptions
        {
            AccountSid = FirstReal(overlay.TwilioAccountSid, baseline.AccountSid),
            ApiKey = FirstReal(overlay.TwilioApiKey, baseline.ApiKey),
            ApiSecret = FirstReal(overlay.TwilioApiSecret, baseline.ApiSecret),
            AuthToken = FirstReal(overlay.TwilioAuthToken, baseline.AuthToken),
            FromNumber = FirstReal(overlay.TwilioFromNumber, baseline.FromNumber)
        };
    }

    private static string FirstReal(string? overlay, string baseline) =>
        !string.IsNullOrWhiteSpace(overlay) ? overlay.Trim() : baseline ?? string.Empty;

    private static void ApplyIfProvided(Action<string?> assign, string? value)
    {
        if (value is null)
        {
            return;
        }

        assign(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    private static bool IsMask(string value) =>
        value.All(c => c == '•' || c == '*' || c == '·');
}

public class IntegrationsPublicStatus
{
    public string ExchangeTenantId { get; set; } = string.Empty;
    public string ExchangeClientId { get; set; } = string.Empty;
    public string ExchangeSenderAddress { get; set; } = string.Empty;
    public bool ExchangeClientSecretConfigured { get; set; }
    public bool ExchangeConfigured { get; set; }

    public string TwilioAccountSid { get; set; } = string.Empty;
    public string TwilioApiKey { get; set; } = string.Empty;
    public string TwilioFromNumber { get; set; } = string.Empty;
    public bool TwilioApiSecretConfigured { get; set; }
    public bool TwilioAuthTokenConfigured { get; set; }
    public bool TwilioConfigured { get; set; }

    public bool OverlayFileExists { get; set; }
}

