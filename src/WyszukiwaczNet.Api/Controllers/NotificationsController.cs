using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Jobs;
using WyszukiwaczNet.Api.Notifications;
using WyszukiwaczNet.Api.Services;

namespace WyszukiwaczNet.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationJob _notificationJob;
    private readonly ISmsProvider _smsProvider;
    private readonly IDiscordProvider _discordProvider;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationJob notificationJob,
        ISmsProvider smsProvider,
        IDiscordProvider discordProvider,
        IEmailService emailService,
        ILogger<NotificationsController> logger)
    {
        _notificationJob = notificationJob;
        _smsProvider = smsProvider;
        _discordProvider = discordProvider;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpPost("cronJob")]
    public IActionResult ScheduleNotificationJob([FromBody] NotificationRequest request)
    {
        try
        {
            if (request.UserId <= 0)
                return BadRequest(new { message = "ID użytkownika jest wymagane." });

            string jobId;
            if (!string.IsNullOrEmpty(request.HourToSendMail) || request.RepeatAfterSpecifiedTime > 0)
            {
                jobId = _notificationJob.EnqueueRecurringJob(request);
            }
            else
            {
                jobId = _notificationJob.EnqueueJob(request);
            }

            return Created($"/api/notifications/{jobId}", new { status = "OK", jobId, message = "Job zarejestrowany prawidłowo." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas planowania zadania powiadomień.");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("jobsForUser/{userId}")]
    public IActionResult GetJobsForUser(int userId)
    {
        try
        {
            var jobs = _notificationJob.GetJobsForUser(userId);
            return Ok(new { success = true, data = jobs });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania zadań użytkownika {UserId}", userId);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("deleteJobsForUser")]
    public IActionResult DeleteJobsForUser([FromBody] int userId)
    {
        try
        {
            var count = _notificationJob.DeleteJobsForUser(userId);
            return Ok(new { status = "OK", success = true, message = $"Usunięto {count} zadania dla użytkownika {userId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas usuwania zadań użytkownika {UserId}", userId);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("sms")]
    public async Task<IActionResult> SendSms([FromBody] SmsRequest request)
    {
        try
        {
            await _smsProvider.SendSmsAsync(request.To, request.Message);
            return Ok(new { success = true, message = "Wiadomość SMS została wysłana pomyślnie!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wystąpił błąd podczas wysyłania wiadomości SMS");
            return StatusCode(500, new { success = false, message = "Wystąpił błąd podczas wysyłania wiadomości SMS.", error = ex.Message });
        }
    }

    [HttpPost("discord")]
    public async Task<IActionResult> SendDiscord([FromBody] DiscordRequest request)
    {
        try
        {
            await _discordProvider.SendMessageAsync(request.Message);
            return Ok(new { success = true, message = "Wiadomość została pomyślnie wysłana na Discord!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wystąpił błąd podczas wysyłania wiadomości na Discord");
            return StatusCode(500, new { success = false, message = "Podczas wysyłania wiadomości wystąpił błąd.", error = ex.Message });
        }
    }

    [HttpPost("email")]
    public async Task<IActionResult> SendEmail([FromBody] EmailRequest request)
    {
        try
        {
            await _emailService.SendEmailAsync(request.To, request.Subject, request.Body);
            return Ok(new { success = true, message = "Email wysłany prawidłowo!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wystąpił błąd podczas wysyłania wiadomości e-mail");
            return StatusCode(500, new { success = false, message = "Wystąpił błąd podczas wysyłania wiadomości e-mail.", error = ex.Message });
        }
    }
}

public record SmsRequest(string To, string Message);
public record DiscordRequest(string Message);
public record EmailRequest(string To, string Subject, string Body);
