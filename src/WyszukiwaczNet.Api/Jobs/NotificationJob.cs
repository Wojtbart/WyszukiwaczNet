using Hangfire;
using Hangfire.Storage;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Entities;
using WyszukiwaczNet.Api.Notifications;
using WyszukiwaczNet.Api.Repositories;
using WyszukiwaczNet.Api.Services;

namespace WyszukiwaczNet.Api.Jobs;

public interface INotificationJob
{
    Task ExecuteAsync(NotificationRequest request);
    string EnqueueJob(NotificationRequest request);
    string EnqueueRecurringJob(NotificationRequest request);
    int DeleteJobsForUser(int userId);
}

public class NotificationJob : INotificationJob
{
    private readonly IPythonScriptService _pythonScriptService;
    private readonly ISmsProvider _smsProvider;
    private readonly IDiscordProvider _discordProvider;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<NotificationJob> _logger;

    private static readonly string JobPrefix = "NotificationJob_UserId";

    public NotificationJob(
        IPythonScriptService pythonScriptService,
        ISmsProvider smsProvider,
        IDiscordProvider discordProvider,
        IEmailService emailService,
        IConfiguration configuration,
        IBackgroundJobClient backgroundJobClient,
        IUserRepository userRepository,
        ILogger<NotificationJob> logger)
    {
        _pythonScriptService = pythonScriptService;
        _smsProvider = smsProvider;
        _discordProvider = discordProvider;
        _emailService = emailService;
        _configuration = configuration;
        _backgroundJobClient = backgroundJobClient;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(NotificationRequest request)
    {
        _logger.LogInformation("Wykonywanie zadania powiadomieñ dla u¿ytkownika {UserId}", request.UserId);

        var scriptsPath = _configuration.GetValue<string>("ScriptsPath") ?? "../../backend";

        var finalPhrase = $"{request.Phrase} {request.AdditionalPhrase}".Trim();

        var allOffers = new List<Offer>();

        foreach (var website in request.Websites)
        {
            var scriptName = GetScriptName(website);
            if (string.IsNullOrEmpty(scriptName)) continue;

            var scriptPath = Path.Combine(scriptsPath, scriptName);

            try
            {
                var (_, offers) = await _pythonScriptService.ExecuteScraperAsync(scriptPath, finalPhrase, website, request.RequestNumber);
                allOffers.AddRange(offers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie uda³o siê wykonaæ skryptu dla {Website}", website);
            }
        }

        if (allOffers.Count == 0)
        {
            _logger.LogInformation("Nie pobrano ¿adnych danych, pomijam powiadomienie");
            return;
        }

        var offersToSend = allOffers;

        if (request.Email)
        {
            await _emailService.SendOffersEmailAsync(request.UserId, offersToSend);
        }

        if (request.Sms)
        {
            var phone = _configuration.GetValue<string>("Notification:DefaultPhone");
            if (!string.IsNullOrEmpty(phone))
            {
                await _smsProvider.SendSmsAsync(phone, $"Znaleziono {offersToSend.Count} nowe oferty!");
            }
        }

        if (request.Discord)
        {
            await _discordProvider.SendMessageAsync($"Znaleziono {offersToSend.Count} nowe oferty dla: {request.Phrase}");
        }

        var feedItems = offersToSend.Select(o => new Notification
        {
            UserId = request.UserId,
            OfferId = o.Id,
            Channel = request.Email ? "email" : request.Sms ? "sms" : "discord",
            Status = "new",
            CreatedAt = DateTime.UtcNow
        }).ToList();

        if (feedItems.Count > 0)
            await _userRepository.SaveNotificationFeedItemsAsync(feedItems);

        _logger.LogInformation("Zadanie powiadomienia dla u¿ytkownika zosta³o zakoñczone {UserId}", request.UserId);
    }

    public string EnqueueJob(NotificationRequest request)
    {
        return _backgroundJobClient.Enqueue(() => ExecuteAsync(request));
    }

    public string EnqueueRecurringJob(NotificationRequest request)
    {
        var jobId = BuildJobId(request.UserId, request.Phrase);
        var cronExpression = GetCronExpression(request.HourToSendMail, request.RepeatAfterSpecifiedTime);

        RecurringJob.AddOrUpdate<NotificationJob>(
            jobId,
            job => job.ExecuteAsync(request),
            cronExpression,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time")
            });

        return jobId;
    }

    public int DeleteJobsForUser(int userId)
    {
        var prefix = $"{JobPrefix}_{userId}_";
        var deleted = 0;

        using var connection = JobStorage.Current.GetConnection();
        var recurringJobs = connection.GetRecurringJobs();

        foreach (var job in recurringJobs.Where(j => j.Id.StartsWith(prefix)))
        {
            RecurringJob.RemoveIfExists(job.Id);
            deleted++;
        }

        return deleted;
    }

    private static string BuildJobId(int userId, string phrase)
    {
        var safePhr = new string(phrase.Take(20)
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray()).Trim('_');

        var timestamp = DateTime.Now.ToString("dd_MM_yyyy_HH_mm");
        return $"{JobPrefix}_{userId}_{safePhr}_{timestamp}";
    }

    private static string GetCronExpression(string? time, int? intervalMinutes)
    {
        if (!string.IsNullOrEmpty(time))
        {
            var parts = time.Split(':');
            if (parts.Length == 2)
            {
                var minute = parts[1];
                var hour = parts[0];
                return $"{minute} {hour} * * *";
            }
        }

        if (intervalMinutes.HasValue && intervalMinutes > 0)
        {
            return $"*/{intervalMinutes} * * * *";
        }

        return "0 0 * * *";
    }

    private static string? GetScriptName(string website) => website.ToLower() switch
    {
        "olx" => "marketplace/olx_scrapper.py",
        "amazon" => "marketplace/amazon_scrapper.py",
        "allegro" => "marketplace/allegro_scraper.py",
        "aliexpress" => "marketplace/aliexpress_scrapper.py",
        "ebay" => "marketplace/ebay_scrapper.py",
        "otomoto" => "auto/otomoto_scrapper.py",
        "autoscout" => "auto/autoscout_scrapper.py",
        "gratka" => "auto/gratka_scrapper.py",
        "sprzedajemy" => "auto/sprzedajemy_scrapper.py",
        "autocentrum" => "auto/autocentrum_scrapper.py",
        "otodom" => "apartment/otodom_scrapper.py",
        "pepper" => "promotions/pepper_scrapper.py",
        "carrot" => "promotions/carrot_scrapper.py",
        _ => null
    };
}
