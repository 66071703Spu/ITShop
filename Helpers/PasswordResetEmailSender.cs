using System.Net;
using System.Net.Mail;

namespace ITShop.Helpers;

public sealed class PasswordResetEmailSender(IConfiguration configuration, IWebHostEnvironment environment)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["PasswordReset:Smtp:Host"])
        && !string.IsNullOrWhiteSpace(configuration["PasswordReset:Smtp:From"])
        && GetPublicBaseUrl() != null;

    public string CreateResetUrl(string token)
    {
        var baseUrl = GetPublicBaseUrl()
            ?? throw new InvalidOperationException("A trusted public site URL is required for password resets.");
        return new Uri(baseUrl, $"/Account/ResetPassword?token={Uri.EscapeDataString(token)}").ToString();
    }

    private Uri? GetPublicBaseUrl()
    {
        var value = configuration["PasswordReset:PublicBaseUrl"];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(environment.IsDevelopment()
                && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
        {
            return null;
        }

        return uri;
    }

    public void Send(string recipient, string resetUrl)
    {
        var section = configuration.GetSection("PasswordReset:Smtp");
        var host = section["Host"] ?? throw new InvalidOperationException("SMTP host is missing.");
        var from = section["From"] ?? throw new InvalidOperationException("SMTP sender is missing.");
        var port = section.GetValue<int?>("Port") ?? 587;

        using var message = new MailMessage(from, recipient)
        {
            Subject = "ITShop password reset",
            Body = $"เปิดลิงก์นี้เพื่อตั้งรหัสผ่านใหม่ภายใน 30 นาที:\n{resetUrl}\n\nหากคุณไม่ได้ขอเปลี่ยนรหัสผ่าน ให้ข้ามอีเมลนี้",
            IsBodyHtml = false
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = section.GetValue<bool?>("EnableSsl") ?? true
        };

        var username = section["Username"];
        var password = section["Password"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        client.Send(message);
    }
}
