using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PTCalc.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;

    public SmtpEmailService(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(EmailAccountOptions account, string toEmail, string subject, string body)
    {
        // Yapılandırma eksikse hatayı burada, sebebini söyleyerek veriyoruz.
        // Aksi halde eksik parola mail sunucusundan "535: Login incorrect
        // -ERR no password entered" gibi kriptik bir yanıt olarak dönüyor ve
        // sorunun kodda mı yapılandırmada mı olduğu anlaşılmıyor.
        //
        // Bu pratikte oluyor: sırlar appsettings.json içinde tutuluyor ve
        // depodaki kopyada parolalar boş; dosyayı ezen bir deploy sunucudaki
        // gerçek değerleri siler.
        EnsureConfigured(account);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(account.Name, account.Email));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_options.SmtpServer, _options.SmtpPort, MailKit.Security.SecureSocketOptions.SslOnConnect);
        await smtp.AuthenticateAsync(account.Email, account.Password);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }

    private void EnsureConfigured(EmailAccountOptions account)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpServer))
            throw new InvalidOperationException(
                $"SMTP sunucusu yapılandırılmamış ({EmailOptions.SectionName}:SmtpServer).");

        if (string.IsNullOrWhiteSpace(account.Email))
            throw new InvalidOperationException(
                $"Gönderen e-posta adresi yapılandırılmamış ({EmailOptions.SectionName}).");

        if (string.IsNullOrWhiteSpace(account.Password))
            throw new InvalidOperationException(
                $"{account.Email} için SMTP parolası yapılandırılmamış ({EmailOptions.SectionName}). " +
                "Sunucudaki appsettings.json, depodaki boş kopyayla değişmiş olabilir.");
    }
}
