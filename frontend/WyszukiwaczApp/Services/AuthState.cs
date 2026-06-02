namespace WyszukiwaczApp.Services;

public class AuthState
{
    public bool IsLogged { get; set; }
    public string Login { get; set; } = string.Empty;
    public int? UserId { get; set; }

    // Static backing field so AuthTokenHandler (created in factory DI scope)
    // reads the same token as the Blazor circuit's AuthState instance.
    private static string? _token;
    public string? AuthToken
    {
        get => _token;
        set => _token = value;
    }

    public event Action? NotificationsChanged;
    public void NotifyNotificationsChanged() => NotificationsChanged?.Invoke();
}
