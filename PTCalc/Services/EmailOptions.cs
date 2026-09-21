namespace PTCalc.Services;

public class EmailOptions
{
    public const string SectionName = "EmailSettings";

    public string SmtpServer { get; set; } = "";
    public int SmtpPort { get; set; } = 465;
    public EmailAccountOptions ContactForm { get; set; } = new();
    public EmailAccountOptions NoReply { get; set; } = new();
}

public class EmailAccountOptions
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
