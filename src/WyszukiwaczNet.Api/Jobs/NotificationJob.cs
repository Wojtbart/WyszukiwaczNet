using Hangfire;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Notifications;
using WyszukiwaczNet.Api.Services;

namespace WyszukiwaczNet.Api.Jobs;

public interface INotificationJob
{
    Task ExecuteAsync(NotificationRequest request);
    string EnqueueJob(NotificationRequest request);
    string EnqueueRecurringJob(NotificationRequest request, string jobId);
    bool DeleteJob(string jobId);
}

public class NotificationJob : INotificationJob
{
    private readonly IPythonScriptService _pythonScriptService;
    private readonly ISmsProvider _smsProvider;
    private readonly IDiscordProvider _discordProvider;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<NotificationJob> _logger;

    private static readonly Dictionary<string, (string CronExpression, int UserId)> ActiveJobs = new();

    public NotificationJob(
        IPythonScriptService pythonScriptService,
        ISmsProvider smsProvider,
        IDiscordProvider discordProvider,
        IEmailService emailService,
        IConfiguration configuration,
        IBackgroundJobClient backgroundJobClient,
        ILogger<NotificationJob> logger)
    {
        _pythonScriptService = pythonScriptService;
        _smsProvider = smsProvider;
        _discordProvider = discordProvider;
        _emailService = emailService;
        _configuration = configuration;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task ExecuteAsync(NotificationRequest request)
    {
        _logger.LogInformation("Executing notification job for user {UserId}", request.UserId);

        var scriptsPath = _configuration.GetValue<string>("ScriptsPath") ?? "../../backend";
        
        var finalPhrase = $"{request.Phrase} {request.AdditionalPhrase}".Trim();
        
        var totalRecords = 0;
        
        foreach (var website in request.Websites)
        {
            var scriptName = GetScriptName(website);
            if (string.IsNullOrEmpty(scriptName)) continue;

            var scriptPath = Path.Combine(scriptsPath, scriptName);
            
            try
            {
                var (count, _) = await _pythonScriptService.ExecuteScraperAsync(scriptPath, finalPhrase);
                totalRecords += count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute script for {Website}", website);
            }
        }

        if (totalRecords == 0)
        {
            _logger.LogInformation("No data fetched, skipping notifications");
            return;
        }

        var limit = Math.Min(request.RequestNumber, totalRecords);

        if (request.Email)
        {
            await _emailService.SendNotificationEmailAsync(request.UserId, limit);
        }

        if (request.Sms)
        {
            var phone = _configuration.GetValue<string>("Notification:DefaultPhone");
            if (!string.IsNullOrEmpty(phone))
            {
                await _smsProvider.SendSmsAsync(phone, $"Found {limit} new offers!");
            }
        }

        if (request.Discord)
        {
            await _discordProvider.SendMessageAsync($"Found {limit} new offers for: {request.Phrase}");
        }

        _logger.LogInformation("Notification job completed for user {UserId}", request.UserId);
    }

    public string EnqueueJob(NotificationRequest request)
    {
        return _backgroundJobClient.Enqueue(() => ExecuteAsync(request));
    }

    public string EnqueueRecurringJob(NotificationRequest request, string jobId)
    {
        var cronExpression = GetCronExpression(request.HourToSendMail, request.RepeatAfterSpecifiedTime);
        
        if (ActiveJobs.ContainsKey(jobId))
        {
            RecurringJob.RemoveIfExists(jobId);
        }

        ActiveJobs[jobId] = (cronExpression, request.UserId);

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

    public bool DeleteJob(string jobId)
    {
        if (ActiveJobs.ContainsKey(jobId))
        {
            RecurringJob.RemoveIfExists(jobId);
            ActiveJobs.Remove(jobId);
            return true;
        }
        return false;
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
        "pepper" => "pepper.py",
        "olx" => "olx_scrapper.py",
        "allegro" => "allegro_scraper.py",
        "amazon" => "amazon_scrapper.py",
        "otomoto" => "oto_moto_scrapper.py",
        "otodom" => "oto_dom_scrapper.py",
        "autoscout" => "autoscout_scrapper.py",
        "gratka" => "gratka_scrapper.py",
        "sprzedajemy" => "sprzedajemy_scrapper.py",
        "autocentrum" => "autocentrum_scrapper.py",
        _ => null
    };
}
