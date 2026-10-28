using HomecrestWeatherAlert.Options;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Twilio.Security;

namespace HomecrestWeatherAlert.Controllers;

/// <summary>
/// Inbound SMS webhook for Twilio Messaging. Configure the phone number's
/// "A message comes in" webhook to POST https://{host}/api/twilio/sms
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/twilio")]
public class TwilioSmsWebhookController : ControllerBase
{
    private readonly EmployeeOptOutService _optOut;
    private readonly TwilioOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<TwilioSmsWebhookController> _logger;

    public TwilioSmsWebhookController(
        EmployeeOptOutService optOut,
        IOptions<TwilioOptions> options,
        IHostEnvironment env,
        ILogger<TwilioSmsWebhookController> logger)
    {
        _optOut = optOut;
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    [HttpPost("sms")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> InboundSms(CancellationToken cancellationToken)
    {
        IFormCollection form = Request.HasFormContentType
            ? await Request.ReadFormAsync(cancellationToken)
            : FormCollection.Empty;

        if (!IsTwilioRequestValid(form))
        {
            _logger.LogWarning("Rejected inbound SMS webhook with invalid Twilio signature.");
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var from = form["From"].ToString();
        var body = form["Body"].ToString();

        if (SmsOptOutKeywords.IsOptOut(body))
        {
            await _optOut.ApplyOptOutAsync(from, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Inbound SMS from {Masked} was not an opt-out keyword.", PhoneNormalizer.Mask(from));
        }

        // Empty TwiML — Twilio/carrier already sends the standard opt-out confirmation for STOP.
        const string emptyTwiml = """<?xml version="1.0" encoding="UTF-8"?><Response></Response>""";
        return Content(emptyTwiml, "application/xml");
    }

    private bool IsTwilioRequestValid(IFormCollection form)
    {
        // Signature validation uses the Account Auth Token (not the API key secret).
        if (string.IsNullOrWhiteSpace(_options.AuthToken)
            || _options.AuthToken.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            if (_env.IsDevelopment())
            {
                _logger.LogWarning(
                    "Twilio:AuthToken is not set — skipping signature validation in Development. Set AuthToken in production to validate webhooks.");
                return true;
            }

            _logger.LogError("Twilio:AuthToken is required to validate inbound webhook signatures in this environment.");
            return false;
        }

        var signature = Request.Headers["X-Twilio-Signature"].ToString();
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var url = GetPublicRequestUrl();
        var parameters = form.ToDictionary(p => p.Key, p => p.Value.ToString());
        var validator = new RequestValidator(_options.AuthToken);
        return validator.Validate(url, parameters, signature);
    }

    /// <summary>
    /// Reconstruct the public URL Twilio called (honors reverse-proxy / IIS forwarded headers).
    /// </summary>
    private string GetPublicRequestUrl()
    {
        var request = Request;
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault()
            ?? request.Scheme;
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault()
            ?? request.Headers["X-Original-Host"].FirstOrDefault()
            ?? request.Host.Value;
        return $"{scheme}://{host}{request.PathBase}{request.Path}{request.QueryString}";
    }
}
