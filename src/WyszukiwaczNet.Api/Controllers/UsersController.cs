using Microsoft.AspNetCore.Mvc;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Services;

namespace WyszukiwaczNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("registerUser")]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { success = false, message = "Wymagany jest adres e-mail i has³o." });
        }

        var (success, message) = await _userService.RegisterAsync(request);

        if (!success)
            return Conflict(new { success, message });

        return CreatedAtAction(nameof(Register), new { success, message });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { success = false, message = "Wymagane jest podanie loginu i has³a." });
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
            return BadRequest(new { success = false, message = "Wymagane jest podanie identyfikatora u¿ytkownika i identyfikatora platformy. ." });
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

    [HttpPost("notifications")]
    public async Task<IActionResult> UpdateNotificationSetting([FromBody] NotificationSettingRequest request)
    {
        if (request.UserId <= 0 || request.ChannelId <= 0)
        {
            return BadRequest(new { success = false, message = "Wymagane jest podanie identyfikatora u¿ytkownika i identyfikatora kana³u." });
        }

        var (success, message) = await _userService.UpdateNotificationSettingAsync(request);

        if (!success)
            return BadRequest(new { success, message });

        return Ok(new { success, message });
    }
}
