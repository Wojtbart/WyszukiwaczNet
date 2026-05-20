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
    List<UserJobDto> GetJobsForUser(int userId);
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
        _logger.LogInformation("Wykonywanie zadania powiadomien dla uzytkownika {UserId}", request.UserId);

        var scriptsPath = _configuration.GetValue<string>("ScriptsPath") ?? "../../scripts";

        var finalPhrase = $"{request.Phrase} {request.AdditionalPhrase}".Trim();

        var allOffers = new List<Offer>();

        foreach (var website in request.Websites)
        {
            var scriptName = GetScriptName(website);
            if (string.IsNullOrEmpty(scriptName)) continue;

            var scriptPath = Path.Combine(scriptsPath, scriptName);
            var extraArgs = BuildExtraArgs(website, request);

            try
            {
                var (_, offers) = await _pythonScriptService.ExecuteScraperAsync(scriptPath, finalPhrase, website, request.RequestNumber, extraArgs.Count > 0 ? extraArgs : null);
                allOffers.AddRange(offers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udalo sie wykonac skryptu dla {Website}", website);
            }
        }

        if (allOffers.Count == 0)
        {
            _logger.LogInformation("Nie pobrano zadnych danych, pomijam powiadomienie");
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
            Channel = request.Email ? "email" : request.Sms ? "sms" : request.Discord ? "discord" : "inapp",
            Status = "new",
            CreatedAt = DateTime.UtcNow
        }).ToList();

        if (feedItems.Count > 0)
            await _userRepository.SaveNotificationFeedItemsAsync(feedItems);

        _logger.LogInformation("Zadanie powiadomienia dla uzytkownika zostalo zakonczone {UserId}", request.UserId);
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

    public List<UserJobDto> GetJobsForUser(int userId)
    {
        var prefix = $"{JobPrefix}_{userId}_";

        using var connection = JobStorage.Current.GetConnection();
        var recurringJobs = connection.GetRecurringJobs();

        return recurringJobs
            .Where(j => j.Id.StartsWith(prefix))
            .Select(j =>
            {
                var afterPrefix = j.Id.Length > prefix.Length ? j.Id[prefix.Length..] : string.Empty;
                var parts = afterPrefix.Split('_');
                var phrase = parts.Length > 5
                    ? string.Join(" ", parts[..^5]).Replace("_", " ").Trim()
                    : afterPrefix;

                return new UserJobDto
                {
                    Id = j.Id,
                    Phrase = phrase,
                    Cron = j.Cron,
                    NextExecution = j.NextExecution,
                    LastExecution = j.LastExecution,
                    LastState = j.LastJobState
                };
            })
            .ToList();
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
        "samochody" => "auto/samochody_scrapper.py",
        "otomoto" => "auto/otomoto_scrapper.py",
        "autoscout" => "auto/autoscout_scrapper.py",
        "gratka" => "auto/gratka_scrapper.py",
        "sprzedajemy" => "auto/sprzedajemy_scrapper.py",
        "autocentrum" => "auto/autocentrum_scrapper.py",
        "otodom" => "apartment/otodom_scrapper.py",
        "nieruchomoscionline" => "apartment/nieruchomosci_online_scrapper.py",
        "pepper" => "promotions/pepper_scrapper.py",
        "carrot" => "promotions/carrot_scrapper.py",
        "pracuj" => "work/pracuj_scrapper.py",
        "justjoinit" => "work/justjoinit_scrapper.py",
        "nofluffjobs" => "work/nofluffjobs_scrapper.py",
        "theprotocolit" => "work/theprotocolit_scrapper.py",
        "bulldogjob" => "work/bulldogjob_scrapper.py",
        "solidjobs" => "work/solidjobs_scrapper.py",
        "olxciagniki" => "agriculture/olx_ciagniki_scrapper.py",
        "brzozowiak" => "agriculture/brzozowiak_scrapper.py",
        "sprzedajemyciagniki" => "agriculture/sprzedajemy_ciagniki_scrapper.py",
        "otomotorolnicze" => "agriculture/otomoto_rolnicze_scrapper.py",
        _ => null
    };

    private static List<string> BuildExtraArgs(string website, NotificationRequest request)
    {
        var args = new List<string>();
        var site = website.ToLower();

        var isAuto = site is "otomoto" or "autoscout" or "gratka" or "sprzedajemy" or "autocentrum" or "samochody";
        var isTractor = site is "olxciagniki" or "brzozowiak" or "sprzedajemyciagniki" or "otomotorolnicze";
        var isFlat = site is "otodom" or "nieruchomoscionline";
        var isWork = site is "pracuj" or "justjoinit" or "nofluffjobs" or "theprotocolit" or "bulldogjob" or "solidjobs";

        if ((isAuto || isTractor) && !string.IsNullOrEmpty(request.Fuel))
        { args.Add("--fuel"); args.Add(request.Fuel!); }

        if ((isAuto || isTractor) && !string.IsNullOrEmpty(request.Gearbox))
        { args.Add("--gearbox"); args.Add(request.Gearbox!); }

        if (isAuto && request.EngineCapacityFrom.HasValue)
        { args.Add("--capacity-from"); args.Add(request.EngineCapacityFrom.Value.ToString()); }

        if (isAuto && request.EngineCapacityTo.HasValue)
        { args.Add("--capacity-to"); args.Add(request.EngineCapacityTo.Value.ToString()); }

        if (request.PriceFrom.HasValue)
        { args.Add("--price-from"); args.Add(((int)request.PriceFrom.Value).ToString()); }

        if (request.PriceTo.HasValue)
        { args.Add("--price-to"); args.Add(((int)request.PriceTo.Value).ToString()); }

        if (isFlat && request.AreaFrom.HasValue)
        { args.Add("--area-from"); args.Add(request.AreaFrom.Value.ToString()); }

        if (isFlat && request.AreaTo.HasValue)
        { args.Add("--area-to"); args.Add(request.AreaTo.Value.ToString()); }

        if (isWork && !string.IsNullOrEmpty(request.WorkLocation))
        { args.Add("--loc"); args.Add(request.WorkLocation!); }

        if (site == "pracuj" && request.EmploymentLevel.HasValue)
        { args.Add("--et"); args.Add(request.EmploymentLevel.Value.ToString()); }
        else if (site == "justjoinit" && request.EmploymentLevel.HasValue)
        { args.Add("--el"); args.Add(request.EmploymentLevel.Value.ToString()); }
        else if (isWork && request.EmploymentLevel.HasValue)
        { args.Add("--level"); args.Add(request.EmploymentLevel.Value.ToString()); }

        if (site == "pracuj" && request.ContractType.HasValue)
        { args.Add("--tc"); args.Add(request.ContractType.Value.ToString()); }
        else if (isWork && request.ContractType.HasValue)
        { args.Add("--emp"); args.Add(request.ContractType.Value.ToString()); }

        return args;
    }
}
