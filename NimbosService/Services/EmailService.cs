using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace NimbosService.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendInviteEmailAsync(string toEmail, string parentName, string role)
    {
        var host = _config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation("[Email] SmtpHost not configured — skipping invite email to {Email}", toEmail);
            return;
        }

        var port        = int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 587;
        var username    = _config["Email:Username"] ?? "";
        var password    = _config["Email:Password"] ?? "";
        var fromAddress = _config["Email:FromAddress"] ?? username;
        var fromName    = _config["Email:FromName"] ?? "Nimbos";
        var appStoreUrl = _config["Email:AppStoreUrl"] ?? "https://apps.apple.com/app/nimbos";

        var isCoParent = role == "parent";

        var subject = isCoParent
            ? $"{parentName} invited you to Nimbos as a co-parent"
            : $"{parentName} invited you to join Nimbos";

        var plainText = isCoParent
            ? $"Hi!\n\n{parentName} has invited you to join as a co-parent on Nimbos.\n\n" +
              $"Download the app and ask {parentName} for your invite code.\n\n" +
              $"Download Nimbos: {appStoreUrl}\n\nSee you in the cloud ☁️\nThe Nimbos Team"
            : $"Hi!\n\n{parentName} has invited you to join their family on Nimbos — a habit tracker that turns daily tasks into a fun adventure.\n\n" +
              $"Download the app and ask {parentName} for your invite code.\n\n" +
              $"Download Nimbos: {appStoreUrl}\n\nSee you in the cloud ☁️\nThe Nimbos Team";

        var html = isCoParent
            ? $"""
              <p>Hi!</p>
              <p><strong>{parentName}</strong> has invited you to join as a <strong>co-parent</strong> on <strong>Nimbos</strong>.</p>
              <p>Download the app and ask <strong>{parentName}</strong> for your invite code.</p>
              <p><a href="{appStoreUrl}">Download Nimbos on the App Store ↗</a></p>
              <p>See you in the cloud ☁️<br/>The Nimbos Team</p>
              """
            : $"""
              <p>Hi!</p>
              <p><strong>{parentName}</strong> has invited you to join their family on <strong>Nimbos</strong> — a habit tracker that turns daily tasks into a fun adventure.</p>
              <p>Download the app and ask <strong>{parentName}</strong> for your invite code.</p>
              <p><a href="{appStoreUrl}">Download Nimbos on the App Store ↗</a></p>
              <p>See you in the cloud ☁️<br/>The Nimbos Team</p>
              """;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = plainText, HtmlBody = html }.ToMessageBody();

        using var smtp = new SmtpClient();
        // StartTls: upgrade the connection on port 587; SslOnConnect for port 465.
        var socketOptions = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await smtp.ConnectAsync(host, port, socketOptions);
        if (!string.IsNullOrEmpty(username))
            await smtp.AuthenticateAsync(username, password);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);

        _logger.LogInformation("[Email] Invite email sent to {Email} (role={Role})", toEmail, role);
    }
}
