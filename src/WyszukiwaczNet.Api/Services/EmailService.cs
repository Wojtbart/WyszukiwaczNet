using MimeKit;
using MailKit.Net.Smtp;
using WyszukiwaczNet.Api.Entities;
using WyszukiwaczNet.Api.Repositories;

namespace WyszukiwaczNet.Api.Services;

public interface IEmailService
{
    Task SendNotificationEmailAsync(int userId, int offerCount);
    Task SendOffersEmailAsync(int userId, List<Offer> offers);
    Task SendEmailAsync(string to, string subject, string body);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IEmailTemplateService _templateService;
    private readonly IUserRepository _userRepository;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IEmailTemplateService templateService,
        IUserRepository userRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _templateService = templateService;
        _userRepository = userRepository;
    }

    public async Task SendNotificationEmailAsync(int userId, int offerCount)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.Email))
        {
            _logger.LogWarning("Nie można wysłać wiadomości e-mail: nie znaleziono użytkownika {UserId} lub nie ma on adresu e-mail", userId);
            return;
        }

        var subject = "Znaleziono nowe oferty!";
        var body = $"Znaleziono {offerCount} nowych ofert spełniających Twoje kryteria wyszukiwania.";
        await SendEmailAsync(user.Email, subject, body);
    }

    public async Task SendOffersEmailAsync(int userId, List<Offer> offers)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.Email))
        {
            _logger.LogWarning("Nie można wysłać wiadomości e-mail: nie znaleziono użytkownika {UserId} lub nie ma on adresu e-mail", userId);
            return;
        }

        var html = _templateService.BuildOffersHtml(offers);
        await SendEmailAsync(user.Email, "Oferty Sprzedażowe", html);
    }

    public async Task SendEmailAsync(string to, string subject, string html)
    {
        var message = new MimeMessage();

        var senderMail = _configuration.GetValue<string>("EmailSettings:Sender");
        var senderHost = _configuration.GetValue<string>("EmailSettings:Host");
        var senderPort = _configuration.GetValue<int>("EmailSettings:Port");
        var senderUsername = _configuration.GetValue<string>("EmailSettings:Username");
        var senderPassword= _configuration.GetValue<string>("EmailSettings:Password");


        message.From.Add(new MailboxAddress("Offers", senderMail ?? string.Empty));
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

         _logger.LogInformation("Wiadomość e-mail wysłana do {Email}", to);
    }
}
