using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Services;

namespace WyszukiwaczNet.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("registerUser")]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                return BadRequest(new { success = false, message = "Wymagany jest adres email i hasło." });

            if (string.IsNullOrEmpty(request.Login))
                return BadRequest(new { success = false, message = "Login jest wymagany." });

            var (success, message) = await _userService.RegisterAsync(request);

            if (!success)
                return Conflict(new { success, message });

            return CreatedAtAction(nameof(Register), new { success, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wystąpił błąd podczas rejestracji w {Email}", request.Email);
            return StatusCode(500, new { success = false, message = "Wystąpił błąd podczas rejestracji." });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { success = false, message = "Wymagane jest podanie loginu i hasła." });
        }

        var response = await _userService.LoginAsync(request);

        if (!response.Success)
            return Unauthorized(response);

        return Ok(response);
    }


    [HttpGet("getUserByLogin/{login}")]
    public async Task<IActionResult> GetUserByLogin(string login)
    {
        var response = await _userService.GetUserByLoginAsync(login);

        if (!response.Success)
            return NotFound(response);

        return Ok(response);
    }

    [HttpGet("{userId}/platforms")]
    public async Task<IActionResult> GetUserPlatformSubscriptions(int userId)
    {
        var subscriptions = await _userService.GetUserPlatformSubscriptionsAsync(userId);
        return Ok(new { success = true, data = subscriptions });
    }

    [HttpPost("platforms")]
    public async Task<IActionResult> UpdatePlatformSubscription([FromBody] PlatformSubscriptionRequest request)
    {
        if (request.UserId <= 0 || request.PlatformId <= 0)
        {
            return BadRequest(new { success = false, message = "Wymagane jest podanie identyfikatora u�ytkownika i identyfikatora platformy." });
        }

        var (success, message) = await _userService.UpdatePlatformSubscriptionAsync(request);

        if (!success)
            return BadRequest(new { success, message });

        return Ok(new { success, message });
    }

    [HttpGet("{userId}/notifications")]
    public async Task<IActionResult> GetUserNotificationSettings(int userId)
    {
        var settings = await _userService.GetUserNotificationSettingsAsync(userId);
        return Ok(new { success = true, data = settings });
    }

    [HttpGet("{userId}/config")]
    public async Task<IActionResult> GetNotificationConfig(int userId)
    {
        var config = await _userService.GetNotificationConfigAsync(userId);
        return Ok(new { success = true, data = config });
    }

    [HttpPost("config")]
    public async Task<IActionResult> SaveNotificationConfig([FromBody] SaveNotificationConfigRequest request)
    {
        if (request.UserId <= 0)
            return BadRequest(new { success = false, message = "Wymagane jest podanie identyfikatora użytkownika." });

        var (success, message) = await _userService.SaveNotificationConfigAsync(request);

        if (!success)
            return BadRequest(new { success, message });

        return Ok(new { success, message });
    }

    [HttpGet("{userId}/feed")]
    public async Task<IActionResult> GetNotificationFeed(int userId, [FromQuery] int limit = 100)
    {
        var items = await _userService.GetNotificationFeedAsync(userId, limit);
        var unread = await _userService.GetUnreadNotificationCountAsync(userId);
        return Ok(new { success = true, data = items, unreadCount = unread });
    }

    [HttpPost("{userId}/feed/read")]
    public async Task<IActionResult> MarkFeedRead(int userId)
    {
        await _userService.MarkNotificationsReadAsync(userId);
        return Ok(new { success = true });
    }

    [HttpPost("{userId}/feed/{notificationId}/read")]
    public async Task<IActionResult> MarkSingleRead(int userId, int notificationId)
    {
        await _userService.MarkSingleNotificationReadAsync(notificationId);
        return Ok(new { success = true });
    }

    [HttpPost("notifications")]
    public async Task<IActionResult> UpdateNotificationSetting([FromBody] NotificationSettingRequest request)
    {
        if (request.UserId <= 0 || request.ChannelId <= 0)
        {
            return BadRequest(new { success = false, message = "Wymagane jest podanie identyfikatora u�ytkownika i identyfikatora kanału." });
        }

        var (success, message) = await _userService.UpdateNotificationSettingAsync(request);

        if (!success)
            return BadRequest(new { success, message });

        return Ok(new { success, message });
    }
}
