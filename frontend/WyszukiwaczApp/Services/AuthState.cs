namespace WyszukiwaczApp.Services;

public class AuthState
{
    public bool IsLogged { get; set; }
    public string Login { get; set; } = string.Empty;
    public int? UserId { get; set; }

    public string? AuthToken { get; set; }

    public event Action? NotificationsChanged;
    public void NotifyNotificationsChanged() => NotificationsChanged?.Invoke();
}
