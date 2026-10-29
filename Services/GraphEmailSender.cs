using Azure.Identity;
using HomecrestWeatherAlert.Options;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace HomecrestWeatherAlert.Services;

/// <summary>
/// Sends mail through Exchange Online via Microsoft Graph app-only <c>Mail.Send</c>
/// (client credentials → <c>/users/{SenderAddress}/sendMail</c>).
/// Recreates the Graph client when admin-rotated secrets change.
/// </summary>
public class GraphEmailSender : IEmailSender
{
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

    private readonly IntegrationsSettingsService _settings;
    private readonly ILogger<GraphEmailSender> _logger;
    private readonly object _clientGate = new();
    private GraphServiceClient? _client;
    private int _clientVersion = -1;
    private string? _clientSender;

    public GraphEmailSender(
        IntegrationsSettingsService settings,
        ILogger<GraphEmailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsConfigured => _settings.GetExchangeOnline().IsConfigured;

    public ExchangeOnlineOptions CurrentOptions => _settings.GetExchangeOnline();

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var options = _settings.GetExchangeOnline();
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException(
                "Exchange Online email is not configured. Set TenantId, ClientId, ClientSecret, and SenderAddress under Admin → Integrations (or ExchangeOnline__* env vars).");
        }

        var body = new SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = htmlMessage ?? string.Empty
                },
                ToRecipients =
                [
                    new Recipient
                    {
                        EmailAddress = new EmailAddress { Address = email.Trim() }
                    }
                ]
            },
            SaveToSentItems = false
        };

        try
        {
            var client = GetOrCreateClient(options);
            await client.Users[options.SenderAddress.Trim()].SendMail.PostAsync(body);
            _logger.LogInformation(
                "Sent email via Graph from {From} to {To} subject {Subject}.",
                options.SenderAddress,
                email,
                subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Graph sendMail failed from {From} to {To}.",
                options.SenderAddress,
                email);
            throw;
        }
    }

    /// <summary>Acquires a token and optionally sends a test message (admin health check).</summary>
    public async Task<(bool Ok, string Detail)> TestConnectionAsync(string? testToEmail, CancellationToken cancellationToken = default)
    {
        var options = _settings.GetExchangeOnline();
        if (!options.IsConfigured)
        {
            return (false, "Exchange Online is not fully configured.");
        }

        try
        {
            var credential = new ClientSecretCredential(
                options.TenantId.Trim(),
                options.ClientId.Trim(),
                options.ClientSecret.Trim(),
                new ClientSecretCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });

            var token = await credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(GraphScopes),
                cancellationToken);

            if (string.IsNullOrEmpty(token.Token))
            {
                return (false, "Token acquisition returned an empty access token.");
            }

            if (string.IsNullOrWhiteSpace(testToEmail))
            {
                return (true, $"Token acquired (expires {token.ExpiresOn:u}). Mailbox send not tested — enter a test recipient to send mail.");
            }

            await SendEmailAsync(
                testToEmail.Trim(),
                "Homecrest Weather Alert — test email",
                "<p>This is a test message from the Homecrest Weather Alert Integrations page.</p>" +
                "<p>If you received this, Microsoft Graph <code>Mail.Send</c> is working.</p>");

            return (true, $"Token OK; test email sent to {testToEmail.Trim()} from {options.SenderAddress}.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exchange Online connection test failed.");
            return (false, ex.Message);
        }
    }

    private GraphServiceClient GetOrCreateClient(ExchangeOnlineOptions options)
    {
        lock (_clientGate)
        {
            var version = _settings.Version;
            if (_client is not null
                && _clientVersion == version
                && string.Equals(_clientSender, options.SenderAddress, StringComparison.OrdinalIgnoreCase))
            {
                return _client;
            }

            var credential = new ClientSecretCredential(
                options.TenantId.Trim(),
                options.ClientId.Trim(),
                options.ClientSecret.Trim(),
                new ClientSecretCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
                });

            _client = new GraphServiceClient(credential, GraphScopes);
            _clientVersion = version;
            _clientSender = options.SenderAddress;
            return _client;
        }
    }
}
