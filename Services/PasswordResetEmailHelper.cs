using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace HomecrestWeatherAlert.Services;

/// <summary>Builds Identity password-reset callback URLs and sends them via <see cref="IEmailSender"/>.</summary>
public class PasswordResetEmailHelper
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly GraphEmailSender _graphEmailSender;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PasswordResetEmailHelper(
        UserManager<IdentityUser> userManager,
        IEmailSender emailSender,
        GraphEmailSender graphEmailSender,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _graphEmailSender = graphEmailSender;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsEmailConfigured => _graphEmailSender.IsConfigured;

    public async Task SendPasswordResetLinkAsync(IdentityUser user)
    {
        var email = user.Email
            ?? throw new InvalidOperationException("User has no email address.");

        var http = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context to build reset links.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var callbackUrl = _linkGenerator.GetUriByPage(
            http,
            page: "/Account/ResetPassword",
            handler: null,
            values: new { area = "Identity", code = encoded },
            scheme: http.Request.Scheme,
            host: http.Request.Host);

        if (string.IsNullOrEmpty(callbackUrl))
        {
            throw new InvalidOperationException("Could not generate a password reset URL.");
        }

        var html =
            "<p>A Homecrest Weather Alert administrator requested a password reset for your account.</p>" +
            $"<p><a href=\"{HtmlEncoder.Default.Encode(callbackUrl)}\">Reset your password</a></p>" +
            "<p>If you did not expect this, you can ignore this message.</p>";

        await _emailSender.SendEmailAsync(email, "Reset your Homecrest Weather Alert password", html);
    }

    public async Task SendAccountCreatedAsync(IdentityUser user)
    {
        var email = user.Email
            ?? throw new InvalidOperationException("User has no email address.");

        var http = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context to build reset links.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var callbackUrl = _linkGenerator.GetUriByPage(
            http,
            page: "/Account/ResetPassword",
            handler: null,
            values: new { area = "Identity", code = encoded },
            scheme: http.Request.Scheme,
            host: http.Request.Host);

        var loginUrl = _linkGenerator.GetUriByPage(
            http,
            page: "/Account/Login",
            handler: null,
            values: new { area = "Identity" },
            scheme: http.Request.Scheme,
            host: http.Request.Host);

        var html =
            "<p>An account was created for you on Homecrest Weather Alert.</p>" +
            $"<p><a href=\"{HtmlEncoder.Default.Encode(loginUrl ?? "#")}\">Sign in</a> with the temporary password your administrator shared, " +
            $"or <a href=\"{HtmlEncoder.Default.Encode(callbackUrl ?? "#")}\">set a new password</a>.</p>";

        await _emailSender.SendEmailAsync(email, "Your Homecrest Weather Alert account", html);
    }
}
