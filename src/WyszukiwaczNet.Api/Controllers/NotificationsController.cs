using Microsoft.AspNetCore.Mvc;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Jobs;
using WyszukiwaczNet.Api.Notifications;
using WyszukiwaczNet.Api.Services;

namespace WyszukiwaczNet.Api.Controllers;

[ApiController]
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
                return BadRequest(new { message = "User ID is required." });

            string jobId;
            if (!string.IsNullOrEmpty(request.HourToSendMail) || request.RepeatAfterSpecifiedTime > 0)
            {
                jobId = _notificationJob.EnqueueRecurringJob(request);
            }
            else
            {
                jobId = _notificationJob.EnqueueJob(request);
            }

            return Created($"/api/notifications/{jobId}", new { status = "OK", jobId, message = "Job successfully registered" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling notification job");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("deleteJobsForUser")]
    public IActionResult DeleteJobsForUser([FromBody] int userId)
    {
        try
        {
            var count = _notificationJob.DeleteJobsForUser(userId);
            return Ok(new { status = "OK", success = true, message = $"Deleted {count} job(s) for user {userId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting jobs for user {UserId}", userId);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("sms")]
    public async Task<IActionResult> SendSms([FromBody] SmsRequest request)
    {
        try
        {
            await _smsProvider.SendSmsAsync(request.To, request.Message);
            return Ok(new { success = true, message = "SMS sent successfully!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS");
            return StatusCode(500, new { success = false, message = "An error occurred while sending the SMS.", error = ex.Message });
        }
    }

    [HttpPost("discord")]
    public async Task<IActionResult> SendDiscord([FromBody] DiscordRequest request)
    {
        try
        {
            await _discordProvider.SendMessageAsync(request.Message);
            return Ok(new { success = true, message = "Message sent to Discord successfully!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Discord message");
            return StatusCode(500, new { success = false, message = "An error occurred while sending the message.", error = ex.Message });
        }
    }

    [HttpPost("email")]
    public async Task<IActionResult> SendEmail([FromBody] EmailRequest request)
    {
        try
        {
            await _emailService.SendEmailAsync(request.To, request.Subject, request.Body);
            return Ok(new { success = true, message = "Email wys�any prawid�owo!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email");
            return StatusCode(500, new { success = false, message = "An error occurred while sending the email.", error = ex.Message });
        }
    }
}

public record SmsRequest(string To, string Message);
public record DiscordRequest(string Message);
public record EmailRequest(string To, string Subject, string Body);
