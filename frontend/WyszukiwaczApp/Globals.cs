using System.Globalization;
using System.Text.RegularExpressions;

namespace WyszukiwaczApp
{
    public class Globals
    {
        private static readonly CultureInfo PlCulture = new("pl-PL");

        public static string FormatPrice(decimal? price)
        {
            if (!price.HasValue) return string.Empty;
            return price % 1 == 0
                ? ((long)price.Value).ToString("N0", PlCulture)
                : price.Value.ToString("N2", PlCulture);
        }

        public static bool IsPhraseValid(string? phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase) || phrase.Length > 200)
                return false;
            return Regex.IsMatch(phrase, @"^[\p{L}\p{N}\s\-#.+]+$");
        }
    }
}
