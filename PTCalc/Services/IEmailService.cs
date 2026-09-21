namespace PTCalc.Services;

public interface IEmailService
{
    Task SendAsync(EmailAccountOptions account, string toEmail, string subject, string body);
}
