using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MyApp.Api.Data;

namespace MyApp.Api.Email;

/// <summary>Strongly-typed SMTP options bound from the <c>Smtp</c> configuration section.</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string FromEmail { get; set; } = "no-reply@myapp.local";
    public string FromName { get; set; } = "MyApp";
}

/// <summary>
/// <see cref="IEmailSender{TUser}"/> implementation that sends real SMTP mail to the
/// configured host. In development that host is <b>MailHog</b>, which captures every
/// message and shows it in its web UI — so the confirmation flow is real end to end,
/// no third-party mail provider required. Swap the host/port for a production SMTP
/// relay (or replace this class with SendGrid/Resend/etc.) when you go live.
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    : IEmailSender<AppUser>
{
    private readonly SmtpOptions _opts = options.Value;

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your email",
            $"""
             <h2>Welcome to MyApp</h2>
             <p>Thanks for signing up. Please confirm your email address to activate your account:</p>
             <p><a href="{confirmationLink}"
                   style="display:inline-block;padding:10px 18px;background:#18181b;color:#fff;border-radius:8px;text-decoration:none">
                   Confirm email</a></p>
             <p style="color:#71717a;font-size:12px">If the button doesn't work, paste this link into your browser:<br>{confirmationLink}</p>
             """);

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your password",
            $"""<p>Reset your MyApp password using this link:</p><p><a href="{resetLink}">{resetLink}</a></p>""");

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) =>
        SendAsync(email, "Your password reset code",
            $"<p>Your MyApp password reset code is: <strong>{resetCode}</strong></p>");

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_opts.FromEmail, _opts.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(to);

        using var client = new SmtpClient(_opts.Host, _opts.Port);
        await client.SendMailAsync(message);
        logger.LogInformation("Sent '{Subject}' email to {Recipient} via {Host}:{Port}", subject, to, _opts.Host, _opts.Port);
    }
}
