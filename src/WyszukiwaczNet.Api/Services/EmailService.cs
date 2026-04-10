using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace WyszukiwaczNet.Api.Services;

public interface IEmailService
{
    Task SendNotificationEmailAsync(int userId, int offerCount);
    Task SendEmailAsync(string to, string subject, string body);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendNotificationEmailAsync(int userId, int offerCount)
    {
        var subject = "New Offers Found!";
        var body = $"Found {offerCount} new offers matching your search criteria.";
        
        var email = _configuration.GetValue<string>("Notification:DefaultEmail");
        if (!string.IsNullOrEmpty(email))
        {
            await SendEmailAsync(email, subject, body);
        }
    }

    //public async Task SendEmailAsync(string to, string subject, string body)
    //{
    //    _logger.LogInformation("Sending email to {To}: {Subject}", to, subject);
        
    //    await Task.CompletedTask;
        
    //    _logger.LogInformation("Email sent successfully");
    //}

    public async Task SendEmailAsync(string to, string subject, string html)
    {
        var message = new MimeMessage();

        var senderMail = _configuration.GetValue<string>("EmailSettings:Sender");
        var senderHost = _configuration.GetValue<string>("EmailSettings:Host");
        var senderPort = _configuration.GetValue<int>("EmailSettings:Port");
        var senderUsername = _configuration.GetValue<string>("EmailSettings:Username");
        var senderPassword= _configuration.GetValue<string>("EmailSettings:Password");


        message.From.Add(new MailboxAddress("Offers", senderMail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        message.Body = new TextPart("html")
        {
            Text = html
        };

        using var client = new SmtpClient();

        await client.ConnectAsync(senderHost, senderPort, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(senderUsername, senderPassword);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Email sent to {Email}", to);
    }
}
