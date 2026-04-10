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
            return BadRequest(new { success = false, message = "'Websites' must be an array." });
        }

        if (string.IsNullOrEmpty(request.Phrase))
        {
            return BadRequest(new { success = false, message = "'Phrase' is required and cannot be empty." });
        }

        if (request.RequestNumber <= 0)
        {
            return BadRequest(new { success = false, message = "'RequestNumber' is required and must be greater than 0." });
        }

        var scriptsPath = _configuration.GetValue<string>("ScriptsPath") ?? "../../backend";
        var finalPhrase = $"{request.Phrase} {request.AdditionalPhrase}".Trim();

        var results = new Dictionary<string, object>();

        foreach (var website in request.Websites)
        {
            var scriptName = GetScriptName(website);
            if (string.IsNullOrEmpty(scriptName))
            {
                _logger.LogWarning("Unknown website: {Website}", website);
                continue;
            }

            var scriptPath = Path.Combine(scriptsPath, scriptName);

            try
            {
                var (count, output) = await _pythonScriptService.ExecuteScraperAsync(scriptPath, finalPhrase);
                string key = website[0].ToString().ToUpper() + website.Substring(1);
                results[$"{key}Data"] = new { count, output };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing script for {Website}", website);
                string key = website[0].ToString().ToUpper() + website.Substring(1);
                results[$"{key}Data"] = new { error = ex.Message };
            }
        }

        return Created("/api/data/getData", new { success = true, message = "Data retrieved successfully!", data = results });
    }

    private static string? GetScriptName(string website) => website.ToLower() switch
    {
        "olx" => "olx_scrapper.py",
        "amazon" => "amazon_scrapper.py",
        "otomoto" => "otomoto_scrapper.py",
        "otodom" => "otodom_scrapper.py",
        "autoscout" => "autoscout_scrapper.py",
        "gratka" => "gratka_scrapper.py",
        "sprzedajemy" => "sprzedajemy_scrapper.py",
        "autocentrum" => "autocentrum_scrapper.py",
        _ => null
    };
}
