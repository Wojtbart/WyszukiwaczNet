using System.Text;
using WyszukiwaczNet.Api.Entities;

namespace WyszukiwaczNet.Api.Services;
public interface IEmailTemplateService
{
    string BuildOffersHtml(List<Offer> offers);
}

public class EmailTemplateService : IEmailTemplateService
{
    private static readonly Dictionary<string, string> PlatformColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OLX"]         = "#00B140",
        ["Allegro"]     = "#FF6600",
        ["Amazon"]      = "#FF9900",
        ["OtoMoto"]     = "#E4002B",
        ["OtoDom"]      = "#0057A8",
        ["AutoScout"]   = "#003399",
        ["Gratka"]      = "#D4001A",
        ["Sprzedajemy"] = "#7B2D8B",
        ["Autocentrum"] = "#1565C0",
        ["Pepper"]      = "#E91E63",
    };

    private static string PlatformColor(string name) =>
        PlatformColors.TryGetValue(name, out var c) ? c : "#374151";

    public string BuildOffersHtml(List<Offer> offers)
    {
        var sb = new StringBuilder();
        var grouped = offers.GroupBy(o => o.Platform?.Name ?? "Inne").ToList();
        var generatedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

        sb.Append($$"""
            <!DOCTYPE html>
            <html lang="pl">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>Oferty Sprzedażowe</title>
              <style>
                /* ── Reset ── */
                *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

                body {
                  background-color: #f0f4f8;
                  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                  color: #1f2937;
                  padding: 0;
                }

                /* ── Outer wrapper ── */
                .email-wrap {
                  max-width: 900px;
                  margin: 0 auto;
                  padding: 32px 16px 48px;
                }

                /* ── Header ── */
                .email-header {
                  background: linear-gradient(135deg, #1d4ed8 0%, #06b6d4 100%);
                  border-radius: 16px 16px 0 0;
                  padding: 36px 32px 28px;
                  text-align: center;
                }
                .email-header h1 {
                  font-size: 1.75rem;
                  font-weight: 800;
                  color: #ffffff;
                  letter-spacing: -0.02em;
                  margin-bottom: 6px;
                }
                .email-header p {
                  color: rgba(255,255,255,0.75);
                  font-size: 0.9rem;
                }
                .header-badges {
                  display: flex;
                  justify-content: center;
                  flex-wrap: wrap;
                  gap: 8px;
                  margin-top: 18px;
                }
                .header-badge {
                  background: rgba(255,255,255,0.18);
                  color: #fff;
                  padding: 4px 12px;
                  border-radius: 999px;
                  font-size: 0.78rem;
                  font-weight: 600;
                  border: 1px solid rgba(255,255,255,0.3);
                }

                /* ── Body card ── */
                .email-body {
                  background: #ffffff;
                  border-radius: 0 0 16px 16px;
                  padding: 32px 24px;
                  box-shadow: 0 4px 24px rgba(0,0,0,0.08);
                }

                /* ── Platform section ── */
                .platform-section {
                  margin-bottom: 40px;
                }
                .platform-section:last-child { margin-bottom: 0; }

                .platform-title {
                  display: flex;
                  align-items: center;
                  gap: 10px;
                  margin-bottom: 14px;
                }
                .platform-dot {
                  width: 12px;
                  height: 12px;
                  border-radius: 50%;
                  flex-shrink: 0;
                }
                .platform-name {
                  font-size: 1.1rem;
                  font-weight: 700;
                  color: #1f2937;
                }
                .platform-count {
                  margin-left: auto;
                  font-size: 0.78rem;
                  color: #6b7280;
                  background: #f3f4f6;
                  padding: 2px 10px;
                  border-radius: 999px;
                  font-weight: 600;
                }

                /* ── Table ── */
                .offer-table {
                  width: 100%;
                  border-collapse: collapse;
                  font-size: 0.875rem;
                  border-radius: 12px;
                  overflow: hidden;
                  box-shadow: 0 1px 6px rgba(0,0,0,0.07);
                }
                .offer-table thead tr {
                  color: #ffffff;
                  font-size: 0.75rem;
                  font-weight: 700;
                  text-transform: uppercase;
                  letter-spacing: 0.05em;
                }
                .offer-table thead th {
                  padding: 12px 14px;
                  text-align: left;
                  border: none;
                }
                .offer-table tbody td {
                  padding: 11px 14px;
                  border-bottom: 1px solid #f1f5f9;
                  vertical-align: middle;
                  color: #374151;
                }
                .offer-table tbody tr:last-child td { border-bottom: none; }
                .offer-table tbody tr:nth-child(even) td { background-color: #f8fafc; }

                /* ── Cell helpers ── */
                .cell-title {
                  font-weight: 600;
                  color: #111827;
                  max-width: 220px;
                }
                .cell-price {
                  font-weight: 700;
                  color: #059669;
                  white-space: nowrap;
                }
                .cell-muted { color: #9ca3af; font-size: 0.8rem; font-style: italic; }

                .btn-link {
                  display: inline-block;
                  padding: 5px 14px;
                  border-radius: 6px;
                  color: #ffffff;
                  text-decoration: none;
                  font-weight: 600;
                  font-size: 0.78rem;
                  white-space: nowrap;
                }

                .offer-img {
                  width: 64px;
                  height: 48px;
                  object-fit: cover;
                  border-radius: 6px;
                  display: block;
                }

                /* ── Footer ── */
                .email-footer {
                  text-align: center;
                  margin-top: 28px;
                  color: #9ca3af;
                  font-size: 0.78rem;
                  line-height: 1.6;
                }
                .email-footer a { color: #6b7280; }
              </style>
            </head>
            <body>
              <div class="email-wrap">

                <!-- Header -->
                <div class="email-header">
                  <h1>&#128269; Oferty Sprzedażowe</h1>
                  <p>Znaleziono <strong>{{offers.Count}}</strong> ofert{{OfferSuffix(offers.Count)}} pasujących do Twoich kryteriów</p>
                  <div class="header-badges">
            """);

        foreach (var g in grouped)
        {
            sb.Append($"""<span class="header-badge">{HtmlEncode(g.Key)} ({g.Count()})</span>""");
        }

        sb.Append($$"""
                  </div>
                </div>

                <!-- Body -->
                <div class="email-body">
            """);

        foreach (var group in grouped)
        {
            var platformName = group.Key;
            var platformOffers = group.ToList();
            var color = PlatformColor(platformName);

            sb.Append($"""
                  <div class="platform-section">
                    <div class="platform-title">
                      <span class="platform-dot" style="background:{color};"></span>
                      <span class="platform-name">{HtmlEncode(platformName)}</span>
                      <span class="platform-count">{platformOffers.Count} ofert</span>
                    </div>
                    <table class="offer-table">
                """);

            sb.Append($"""<thead style="background:{color};">""");
            AppendTableHeader(sb, platformName);
            sb.Append("</thead><tbody>");
            AppendTableRows(sb, platformName, platformOffers, color);
            sb.Append("</tbody></table></div>");
        }

        sb.Append($"""
                </div>

                <!-- Footer -->
                <div class="email-footer">
                  <p>Wygenerowano automatycznie &middot; {generatedAt}</p>
                  <p>Nie odpowiadaj na tę wiadomość &middot; Wyszukiwacz Net</p>
                </div>

              </div>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    private static void AppendTableHeader(StringBuilder sb, string platformName)
    {
        if (IsVehiclePlatform(platformName))
        {
            if (platformName.Equals("AutoScout", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("""
                    <tr>
                      <th>Tytuł</th>
                      <th>Cena</th>
                      <th>Przebieg</th>
                      <th>Lokalizacja</th>
                      <th>Link</th>
                    </tr>
                    """);
            }
            else
            {
                sb.Append("""
                    <tr>
                      <th>Tytuł</th>
                      <th>Cena</th>
                      <th>Rok prod.</th>
                      <th>Przebieg</th>
                      <th>Lokalizacja</th>
                      <th>Link</th>
                    </tr>
                    """);
            }
        }
        else if (platformName.Equals("OLX", StringComparison.OrdinalIgnoreCase)
              || platformName.Equals("Sprzedajemy", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("""
                <tr>
                  <th>Tytuł</th>
                  <th>Cena</th>
                  <th>Lokalizacja</th>
                  <th>Link</th>
                </tr>
                """);
        }
        else
        {
            sb.Append("""
                <tr>
                  <th>Tytuł</th>
                  <th>Cena</th>
                  <th>Sprzedający</th>
                  <th>Lokalizacja</th>
                  <th>Zdjęcie</th>
                  <th>Link</th>
                </tr>
                """);
        }
    }

    private static void AppendTableRows(StringBuilder sb, string platformName, List<Offer> offers, string color)
    {
        if (IsVehiclePlatform(platformName))
        {
            bool isAutoScout = platformName.Equals("AutoScout", StringComparison.OrdinalIgnoreCase);
            foreach (var offer in offers)
            {
                sb.Append("<tr>");
                sb.Append($"<td class=\"cell-title\">{HtmlEncode(offer.Title)}</td>");
                sb.Append($"<td class=\"cell-price\">{FormatPrice(offer.Price, offer.Currency)}</td>");
                if (!isAutoScout)
                    sb.Append($"<td>{Val(offer.VehicleDetail?.ProductionYear?.ToString())}</td>");
                sb.Append($"<td>{Val(offer.VehicleDetail?.Mileage is int m ? $"{m:N0} km" : null)}</td>");
                sb.Append($"<td>{Val(offer.Location)}</td>");
                sb.Append($"<td>{Link(offer.Url, color)}</td>");
                sb.Append("</tr>");
            }
        }
        else if (platformName.Equals("OLX", StringComparison.OrdinalIgnoreCase)
              || platformName.Equals("Sprzedajemy", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var offer in offers)
            {
                sb.Append("<tr>");
                sb.Append($"<td class=\"cell-title\">{HtmlEncode(offer.Title)}</td>");
                sb.Append($"<td class=\"cell-price\">{FormatPrice(offer.Price, offer.Currency)}</td>");
                sb.Append($"<td>{Val(offer.Location)}</td>");
                sb.Append($"<td>{Link(offer.Url, color)}</td>");
                sb.Append("</tr>");
            }
        }
        else
        {
            foreach (var offer in offers)
            {
                sb.Append("<tr>");
                sb.Append($"<td class=\"cell-title\">{HtmlEncode(offer.Title)}</td>");
                sb.Append($"<td class=\"cell-price\">{FormatPrice(offer.Price, offer.Currency)}</td>");
                sb.Append($"<td>{Val(offer.SellerName)}</td>");
                sb.Append($"<td>{Val(offer.Location)}</td>");
                sb.Append($"<td>{Image(offer.ImageUrl)}</td>");
                sb.Append($"<td>{Link(offer.Url, color)}</td>");
                sb.Append("</tr>");
            }
        }
    }

    private static bool IsVehiclePlatform(string name) =>
        name.Equals("OtoMoto", StringComparison.OrdinalIgnoreCase)
        || name.Equals("OtoDom", StringComparison.OrdinalIgnoreCase)
        || name.Equals("AutoScout", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Gratka", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Autocentrum", StringComparison.OrdinalIgnoreCase);

    private static string Val(string? value) =>
        string.IsNullOrEmpty(value)
            ? "<span class=\"cell-muted\">—</span>"
            : HtmlEncode(value);

    private static string FormatPrice(decimal? price, string? currency) =>
        price.HasValue
            ? HtmlEncode($"{price:N0} {currency ?? "PLN"}")
            : "<span class=\"cell-muted\">—</span>";

    private static string Link(string? url, string color) =>
        string.IsNullOrEmpty(url)
            ? "<span class=\"cell-muted\">—</span>"
            : $"<a class=\"btn-link\" href=\"{HtmlEncode(url)}\" target=\"_blank\" rel=\"noopener noreferrer\" style=\"background:{color};\">Zobacz</a>";

    private static string Image(string? url) =>
        string.IsNullOrEmpty(url)
            ? "<span class=\"cell-muted\">—</span>"
            : $"<a href=\"{HtmlEncode(url)}\" target=\"_blank\" rel=\"noopener noreferrer\"><img class=\"offer-img\" src=\"{HtmlEncode(url)}\" alt=\"zdjęcie\" /></a>";

    private static string OfferSuffix(int count) => count switch
    {
        1 => "y",
        >= 2 and <= 4 => "y",
        _ => ""
    };

    private static string HtmlEncode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
