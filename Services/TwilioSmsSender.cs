using HomecrestWeatherAlert.Models;
using HomecrestWeatherAlert.Options;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace HomecrestWeatherAlert.Services;

public class TwilioSmsSender : ISmsSender
{
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(IOptions<TwilioOptions> options, ILogger<TwilioSmsSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<IReadOnlyList<SmsSendResult>> SendIndividualAsync(
        string body,
        IEnumerable<(int EmployeeId, string Name, string PhoneE164)> recipients,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Twilio is not configured. Set Twilio:AccountSid, Twilio:ApiKey, Twilio:ApiSecret, and Twilio:FromNumber via User Secrets, environment variables, or appsettings.");
        }

        InitializeTwilioClient(_options, _logger);
        var from = new PhoneNumber(_options.FromNumber);
        var results = new List<SmsSendResult>();

        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new SmsSendResult
            {
                EmployeeId = recipient.EmployeeId,
                EmployeeName = recipient.Name,
                PhoneMasked = PhoneNormalizer.Mask(recipient.PhoneE164)
            };

            try
            {
                // One Create call per recipient — never pass multiple destinations.
                var message = await MessageResource.CreateAsync(
                    to: new PhoneNumber(recipient.PhoneE164),
                    from: from,
                    body: body);

                result.Success = true;
                result.TwilioSid = message.Sid;
                _logger.LogInformation(
                    "SMS sent to employee {EmployeeId} ({MaskedPhone}), SID {Sid}",
                    recipient.EmployeeId,
                    result.PhoneMasked,
                    message.Sid);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                _logger.LogWarning(
                    ex,
                    "SMS failed for employee {EmployeeId} ({MaskedPhone})",
                    recipient.EmployeeId,
                    result.PhoneMasked);
            }

            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Prefer API key auth: TwilioClient.Init(apiKey, apiSecret, accountSid).
    /// Fall back to Account SID + Auth Token only when API key is not configured.
    /// </summary>
    internal static void InitializeTwilioClient(TwilioOptions options, ILogger? logger = null)
    {
        if (options.PreferApiKey)
        {
            TwilioClient.Init(options.ApiKey, options.ApiSecret, options.AccountSid);
            logger?.LogDebug("Twilio client initialized with API key credentials.");
            return;
        }

        if (options.HasAuthTokenCredentials)
        {
            TwilioClient.Init(options.AccountSid, options.AuthToken);
            logger?.LogWarning(
                "Twilio client initialized with legacy Auth Token. Prefer Twilio:ApiKey + Twilio:ApiSecret.");
            return;
        }

        throw new InvalidOperationException("Twilio credentials are incomplete.");
    }
}
