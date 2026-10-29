using HomecrestWeatherAlert.Services;

namespace HomecrestWeatherAlert.Tests;

public class IntegrationsSettingsServiceTests
{
    [Fact]
    public void SaveExchange_rotates_secret_without_redeploy_and_masks_on_status()
    {
        var settings = TestIntegrations.CreateSettings();

        settings.SaveExchangeOnline(
            "11111111-1111-1111-1111-111111111111",
            "22222222-2222-2222-2222-222222222222",
            "super-secret-value",
            "noreply@homecrest.example",
            clearClientSecret: false);

        var live = settings.GetExchangeOnline();
        Assert.True(live.IsConfigured);
        Assert.Equal("super-secret-value", live.ClientSecret);

        var status = settings.GetPublicStatus();
        Assert.True(status.ExchangeConfigured);
        Assert.True(status.ExchangeClientSecretConfigured);
        Assert.True(status.OverlayFileExists);
        Assert.Equal("noreply@homecrest.example", status.ExchangeSenderAddress);

        // Rotate secret; leave mask-equivalent blank by passing the UI mask (should keep old) then real rotate.
        settings.SaveExchangeOnline(
            "11111111-1111-1111-1111-111111111111",
            "22222222-2222-2222-2222-222222222222",
            "••••••••",
            "noreply@homecrest.example",
            clearClientSecret: false);
        Assert.Equal("super-secret-value", settings.GetExchangeOnline().ClientSecret);

        settings.SaveExchangeOnline(
            "11111111-1111-1111-1111-111111111111",
            "22222222-2222-2222-2222-222222222222",
            "rotated-secret",
            "noreply@homecrest.example",
            clearClientSecret: false);
        Assert.Equal("rotated-secret", settings.GetExchangeOnline().ClientSecret);

        // Reload from encrypted file
        settings.ReloadFromDisk();
        Assert.Equal("rotated-secret", settings.GetExchangeOnline().ClientSecret);
    }

    [Fact]
    public void Overlay_overrides_configuration_baseline()
    {
        var settings = TestIntegrations.CreateSettings(new Dictionary<string, string?>
        {
            ["Twilio:AccountSid"] = "ACaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            ["Twilio:ApiKey"] = "SKbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            ["Twilio:ApiSecret"] = "from-config",
            ["Twilio:FromNumber"] = "+15551234567"
        });

        Assert.True(settings.GetTwilio().IsConfigured);
        Assert.Equal("from-config", settings.GetTwilio().ApiSecret);

        settings.SaveTwilio(
            accountSid: null,
            apiKey: null,
            apiSecret: "from-overlay",
            authToken: null,
            fromNumber: null,
            clearApiSecret: false,
            clearAuthToken: false);

        Assert.Equal("from-overlay", settings.GetTwilio().ApiSecret);
        Assert.Equal("+15551234567", settings.GetTwilio().FromNumber);
    }
}
