namespace EgePharmTech.Services;

public interface IEmailService
{
    Task SendAsync(EmailAccountOptions account, string toEmail, string subject, string body);
}
