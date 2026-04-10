namespace WyszukiwaczApp
{
    public class Globals
    {
        public static bool IsLogged { get; set; } = false;
        public static string Login { get; set; } = string.Empty;
        public static int? UserId { get; set; }
        public static string? AuthToken { get; set; }
    }
}
