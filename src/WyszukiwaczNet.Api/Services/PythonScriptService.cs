using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using WyszukiwaczNet.Api.Entities;
using WyszukiwaczNet.Api.Repositories;

namespace WyszukiwaczNet.Api.Services;

public interface IPythonScriptService
{
    Task<(int RecordsCount, List<Offer> Output)> ExecuteScraperAsync(string scriptPath, string phrase, string platformName, int limit = 100, List<string>? extraArgs = null, CancellationToken cancellationToken = default);
}

public class PythonScriptService : IPythonScriptService
{
    private readonly string _pythonPath;
    private readonly string? _dbConfigKey;
    private readonly ILogger<PythonScriptService> _logger;
    private readonly IOfferRepository _offerRepository;

    public PythonScriptService(IConfiguration configuration, ILogger<PythonScriptService> logger, IOfferRepository offerRepository)
    {
        _pythonPath = configuration.GetValue<string>("PythonPath") ?? "python";
        _dbConfigKey = configuration.GetValue<string>("DbConfigKey");
        _logger = logger;
        _offerRepository = offerRepository;
    }

    public async Task<(int RecordsCount, List<Offer> Output)> ExecuteScraperAsync(
        string scriptPath,
        string phrase,
        string platformName,
        int limit = 100,
        List<string>? extraArgs = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsPhraseValid(phrase))
        {
            _logger.LogWarning("Odrzucono niebezpieczna fraze: {Phrase}", phrase);
            throw new ArgumentException("Fraza zawiera niedozwolone znaki.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (!string.IsNullOrEmpty(_dbConfigKey))
            startInfo.EnvironmentVariables["DB_CONFIG_KEY"] = _dbConfigKey;
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add(phrase);
        if (extraArgs != null)
            foreach (var arg in extraArgs)
                startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        var offers = new List<Offer>();
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        var extraArgsStr = extraArgs != null && extraArgs.Count > 0 ? string.Join(" ", extraArgs) : "(brak)";
        _logger.LogInformation("Uruchomiono skrypt Python: {Script}, fraza: {Phrase}, args: {Args}", scriptPath, phrase, extraArgsStr);

        var startTime = DateTime.UtcNow;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() => process.WaitForExit(120000), cancellationToken);

        if (!completed)
        {
            process.Kill(true);
            _logger.LogError("Przekroczono limit czasu skryptu w jezyku Python: {Script}", scriptPath);
            throw new TimeoutException($"Wykonywanie skryptu przekroczylo limit czasu: {scriptPath}");
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (!string.IsNullOrEmpty(output))
            _logger.LogInformation("Skrypt stdout: {Output}", output);
        if (!string.IsNullOrEmpty(error))
            _logger.LogWarning("Skrypt stderr: {Error}", error);

        var recordsCount = ParseRecordsCount(output);

        _logger.LogInformation("Skrypt {Script} zakończono, uzyskano {Count} rekordów", scriptPath, recordsCount);

        if (recordsCount != 0)
        {
            var platform = await _offerRepository.GetPlatformByNameAsync(platformName.ToLower());
            if (platform != null)
                offers = await _offerRepository.GetNewOffersByPlatformAsync(platform.Id, startTime, limit);
            else
                offers = await _offerRepository.GetRecentOffersAsync(limit);
        }

        return (recordsCount, offers);
    }

    private static bool IsPhraseValid(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase) || phrase.Length > 200)
            return false;
        return Regex.IsMatch(phrase, @"^[\p{L}\p{N}\s\-#.+]+$");
    }

    private static int ParseRecordsCount(string output)
    {
        var match = Regex.Match(output, @"Records:\s*(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
}
