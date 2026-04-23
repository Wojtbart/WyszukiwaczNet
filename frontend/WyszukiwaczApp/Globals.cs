using System.Text.RegularExpressions;

namespace WyszukiwaczApp
{
    public class Globals
    {
        public static bool IsLogged { get; set; } = false;
        public static string Login { get; set; } = string.Empty;
        public static int? UserId { get; set; }
        public static string? AuthToken { get; set; }

        public static event Action? NotificationsChanged;
        public static void NotifyNotificationsChanged() => NotificationsChanged?.Invoke();

        public static bool IsPhraseValid(string? phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase) || phrase.Length > 200)
                return false;
            return Regex.IsMatch(phrase, @"^[\p{L}\p{N}\s\-]+$");
        }
    }
}
