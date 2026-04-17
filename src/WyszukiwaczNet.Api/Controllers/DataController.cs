using Microsoft.AspNetCore.Mvc;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Services;

namespace WyszukiwaczNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly IPythonScriptService _pythonScriptService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataController> _logger;

    public DataController(
        IPythonScriptService pythonScriptService,
        IConfiguration configuration,
        ILogger<DataController> logger)
    {
        _pythonScriptService = pythonScriptService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("getData")]
    public async Task<IActionResult> GetDataFromPlatforms([FromBody] GetDataRequest request)
    {
        if (request.Websites == null || !request.Websites.Any())
        {
            return BadRequest(new { success = false, message = "'Websites' mus byæ tablic¹." });
        }

        if (string.IsNullOrEmpty(request.Phrase))
        {
            return BadRequest(new { success = false, message = "'Phrase' jest wymagana i nie mo¿e byæ pusta." });
        }

        if (request.RequestNumber <= 0)
        {
            return BadRequest(new { success = false, message = "'RequestNumber' jest wymagana i nie mo¿e byæ mniejszy ni¿ 0." });
        }

        var scriptsPath = _configuration.GetValue<string>("ScriptsPath") ?? "../../backend";
        var finalPhrase = $"{request.Phrase} {request.AdditionalPhrase}".Trim();

        var results = new Dictionary<string, object>();

        foreach (var website in request.Websites)
        {
            var scriptName = GetScriptName(website);
            if (string.IsNullOrEmpty(scriptName))
            {
                _logger.LogWarning("Nieznana strona: {Website}", website);
                continue;
            }

            var scriptPath = Path.Combine(scriptsPath, scriptName);

            try
            {
                var (count, output) = await _pythonScriptService.ExecuteScraperAsync(scriptPath, finalPhrase, website, request.RequestNumber);
                string key = website[0].ToString().ToUpper() + website.Substring(1);
                results[$"{key}Data"] = new { count, output };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "b³ad wykonywania skryptu dla {Website}", website);
                string key = website[0].ToString().ToUpper() + website.Substring(1);
                results[$"{key}Data"] = new { error = ex.Message };
            }
        }

        return Created("/api/data/getData", new { success = true, message = "Dane przetworzone prawid³owo!", data = results });
    }

    private static string? GetScriptName(string website) => website.ToLower() switch
    {
        "olx" => "marketplace/olx_scrapper.py",
        "amazon" => "marketplace/amazon_scrapper.py",
        "allegro" => "marketplace/allegro_scraper.py",
        "aliexpress" => "marketplace/aliexpress_scrapper.py",
        "ebay" => "marketplace/ebay_scrapper.py",
        "otomoto" => "auto/otomoto_scrapper.py",
        "autoscout" => "auto/autoscout_scrapper.py",
        "gratka" => "auto/gratka_scrapper.py",
        "sprzedajemy" => "auto/sprzedajemy_scrapper.py",
        "autocentrum" => "auto/autocentrum_scrapper.py",
        "otodom" => "apartment/otodom_scrapper.py",
        "pepper" => "promotions/pepper_scrapper.py",
        "carrot" => "promotions/carrot_scrapper.py",
        _ => null
    };
}
