using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace WyszukiwaczNet.Api.Notifications;

public interface ISmsProvider
{
    Task SendSmsAsync(string to, string message);
}

public interface IDiscordProvider
{
    Task SendMessageAsync(string message, object? embed = null);
}

public class TwilioSmsProvider : ISmsProvider
{
    private readonly string _fromNumber;
    private readonly ILogger<TwilioSmsProvider> _logger;

    public TwilioSmsProvider(IConfiguration configuration, ILogger<TwilioSmsProvider> logger)
    {
        var accountSid = configuration["Twilio:AccountSid"] ?? "";
        var authToken = configuration["Twilio:AuthToken"] ?? "";
        _fromNumber = configuration["Twilio:FromNumber"] ?? "";

        TwilioClient.Init(accountSid, authToken);
        _logger = logger;
    }

    public async Task SendSmsAsync(string to, string message)
    {
        try
        {
            await MessageResource.CreateAsync(
                to: new Twilio.Types.PhoneNumber(to),
                from: new Twilio.Types.PhoneNumber(_fromNumber),
                body: message
            );
            _logger.LogInformation("SMS wys³any do {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie uda³o siê wys³aæ wiadomoœci SMS do {To}", to);
            throw;
        }
    }
}

public class DiscordWebhookProvider : IDiscordProvider
{
    private readonly string _webhookUrl;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscordWebhookProvider> _logger;

    public DiscordWebhookProvider(IConfiguration configuration, ILogger<DiscordWebhookProvider> logger)
    {
        _webhookUrl = configuration["Discord:WebhookUrl"] ?? "";
        _httpClient = new HttpClient();
        _logger = logger;
    }

    public async Task SendMessageAsync(string message, object? embed = null)
    {
        try
        {
            var payload = new
            {
                content = message,
                embeds = embed != null ? new[] { embed } : null
            };

            await _httpClient.PostAsJsonAsync(_webhookUrl, payload);
            _logger.LogInformation("Wys³ano wiadomoœæ na Discord");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie uda³o siê wys³aæ wiadomoœci na Discord");
            throw;
        }
    }
}
