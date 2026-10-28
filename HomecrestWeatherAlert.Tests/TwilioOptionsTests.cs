using HomecrestWeatherAlert.Options;

namespace HomecrestWeatherAlert.Tests;

public class TwilioOptionsTests
{
    [Fact]
    public void IsConfigured_true_with_api_key_credentials()
    {
        var options = new TwilioOptions
        {
            AccountSid = "ACaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            ApiKey = "SKbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            ApiSecret = "api-secret-value",
            FromNumber = "+15551234567"
        };

        Assert.True(options.HasApiKeyCredentials);
        Assert.True(options.PreferApiKey);
        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void PreferApiKey_when_both_api_key_and_auth_token_present()
    {
        var options = new TwilioOptions
        {
            AccountSid = "ACaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            ApiKey = "SKbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            ApiSecret = "api-secret-value",
            AuthToken = "auth-token-value",
            FromNumber = "+15551234567"
        };

        Assert.True(options.HasApiKeyCredentials);
        Assert.True(options.HasAuthTokenCredentials);
        Assert.True(options.PreferApiKey);
    }

    [Fact]
    public void AuthToken_alone_is_temporary_fallback()
    {
        var options = new TwilioOptions
        {
            AccountSid = "ACaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            AuthToken = "auth-token-value",
            FromNumber = "+15551234567"
        };

        Assert.False(options.HasApiKeyCredentials);
        Assert.False(options.PreferApiKey);
        Assert.True(options.HasAuthTokenCredentials);
        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void Placeholders_are_not_configured()
    {
        var options = new TwilioOptions
        {
            AccountSid = "YOUR_TWILIO_ACCOUNT_SID",
            ApiKey = "YOUR_TWILIO_API_KEY_SID",
            ApiSecret = "YOUR_TWILIO_API_SECRET",
            FromNumber = "YOUR_TWILIO_FROM_NUMBER"
        };

        Assert.False(options.IsConfigured);
        Assert.False(options.PreferApiKey);
    }
}
