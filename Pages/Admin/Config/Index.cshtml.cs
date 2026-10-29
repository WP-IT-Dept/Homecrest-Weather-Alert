using System.ComponentModel.DataAnnotations;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomecrestWeatherAlert.Pages.Admin.Config;

public class IndexModel : PageModel
{
    public const string SecretMask = "••••••••";

    private readonly IntegrationsSettingsService _settings;
    private readonly GraphEmailSender _emailSender;

    public IndexModel(IntegrationsSettingsService settings, GraphEmailSender emailSender)
    {
        _settings = settings;
        _emailSender = emailSender;
    }

    public IntegrationsPublicStatus Status { get; private set; } = new();

    [BindProperty]
    public ExchangeInputModel Exchange { get; set; } = new();

    [BindProperty]
    public TwilioInputModel Twilio { get; set; } = new();

    [BindProperty]
    public TestEmailInputModel TestEmail { get; set; } = new();

    public class ExchangeInputModel
    {
        [Display(Name = "Tenant ID")]
        public string TenantId { get; set; } = string.Empty;

        [Display(Name = "Client ID")]
        public string ClientId { get; set; } = string.Empty;

        [Display(Name = "Client secret")]
        [DataType(DataType.Password)]
        public string ClientSecret { get; set; } = string.Empty;

        [Display(Name = "Sender mailbox")]
        public string SenderAddress { get; set; } = string.Empty;

        [Display(Name = "Clear stored client secret")]
        public bool ClearClientSecret { get; set; }
    }

    public class TwilioInputModel
    {
        [Display(Name = "Account SID")]
        public string AccountSid { get; set; } = string.Empty;

        [Display(Name = "API Key SID")]
        public string ApiKey { get; set; } = string.Empty;

        [Display(Name = "API Key secret")]
        [DataType(DataType.Password)]
        public string ApiSecret { get; set; } = string.Empty;

        [Display(Name = "Auth Token (optional fallback)")]
        [DataType(DataType.Password)]
        public string AuthToken { get; set; } = string.Empty;

        [Display(Name = "From number (E.164)")]
        public string FromNumber { get; set; } = string.Empty;

        [Display(Name = "Clear stored API secret")]
        public bool ClearApiSecret { get; set; }

        [Display(Name = "Clear stored Auth Token")]
        public bool ClearAuthToken { get; set; }
    }

    public class TestEmailInputModel
    {
        [Display(Name = "Test recipient")]
        [EmailAddress]
        public string? To { get; set; }
    }

    public void OnGet() => LoadForm();

    public IActionResult OnPostSaveExchange()
    {
        _settings.SaveExchangeOnline(
            Exchange.TenantId,
            Exchange.ClientId,
            Exchange.ClientSecret,
            Exchange.SenderAddress,
            Exchange.ClearClientSecret);

        TempData["StatusMessage"] = "Exchange Online settings saved (encrypted on disk).";
        return RedirectToPage();
    }

    public IActionResult OnPostSaveTwilio()
    {
        _settings.SaveTwilio(
            Twilio.AccountSid,
            Twilio.ApiKey,
            Twilio.ApiSecret,
            Twilio.AuthToken,
            Twilio.FromNumber,
            Twilio.ClearApiSecret,
            Twilio.ClearAuthToken);

        TempData["StatusMessage"] = "Twilio settings saved (encrypted on disk).";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestExchangeAsync(CancellationToken cancellationToken)
    {
        var (ok, detail) = await _emailSender.TestConnectionAsync(TestEmail.To, cancellationToken);
        TempData[ok ? "StatusMessage" : "ErrorMessage"] = detail;
        return RedirectToPage();
    }

    private void LoadForm()
    {
        Status = _settings.GetPublicStatus();

        Exchange = new ExchangeInputModel
        {
            TenantId = CleanPlaceholder(Status.ExchangeTenantId),
            ClientId = CleanPlaceholder(Status.ExchangeClientId),
            SenderAddress = CleanPlaceholder(Status.ExchangeSenderAddress),
            ClientSecret = Status.ExchangeClientSecretConfigured ? SecretMask : string.Empty
        };

        Twilio = new TwilioInputModel
        {
            AccountSid = CleanPlaceholder(Status.TwilioAccountSid),
            ApiKey = CleanPlaceholder(Status.TwilioApiKey),
            FromNumber = CleanPlaceholder(Status.TwilioFromNumber),
            ApiSecret = Status.TwilioApiSecretConfigured ? SecretMask : string.Empty,
            AuthToken = Status.TwilioAuthTokenConfigured ? SecretMask : string.Empty
        };
    }

    private static string CleanPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value;
}
