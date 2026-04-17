using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Entities;
using WyszukiwaczNet.Api.Repositories;

namespace WyszukiwaczNet.Api.Services;

public interface IPythonScriptService
{
    Task<(int RecordsCount, List<Offer> Output)> ExecuteScraperAsync(string scriptPath, string phrase, string platformName, int limit = 100, CancellationToken cancellationToken = default);
}

public class PythonScriptService : IPythonScriptService
{
    private readonly string _pythonPath;
    private readonly ILogger<PythonScriptService> _logger;
    private readonly IOfferRepository _offerRepository;

    public PythonScriptService(IConfiguration configuration, ILogger<PythonScriptService> logger, IOfferRepository offerRepository)
    {
        _pythonPath = configuration.GetValue<string>("PythonPath") ?? "python";
        _logger = logger;
        _offerRepository = offerRepository;
    }

    public async Task<(int RecordsCount, List<Offer> Output)> ExecuteScraperAsync(
        string scriptPath,
        string phrase,
        string platformName,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"\"{scriptPath}\" \"{phrase}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

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

        _logger.LogInformation("Uruchomiono skrypt w jêzyku Python: {Script} za pomoc¹ frazy: {Phrase}", scriptPath, phrase);

        var startTime = DateTime.UtcNow;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() => process.WaitForExit(120000), cancellationToken);

        if (!completed)
        {
            process.Kill(true);
            _logger.LogError("Przekroczono limit czasu skryptu w jêzyku Python: {Script}", scriptPath);
            throw new TimeoutException($"Wykonywanie skryptu przekroczy³o limit czasu: {scriptPath}");
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (!string.IsNullOrEmpty(error))
            _logger.LogWarning("SKrypt stderr: {Error}", error);

        var recordsCount = ParseRecordsCount(output);

        _logger.LogInformation("Skrypt {Script} zakoñczono, uzyskuj¹c {Count} rekordów", scriptPath, recordsCount);

        if(recordsCount != 0)
        {
            var platform = await _offerRepository.GetPlatformByNameAsync(platformName);
            if (platform != null)
                offers = await _offerRepository.GetNewOffersByPlatformAsync(platform.Id, startTime, limit);
            else
                offers = await _offerRepository.GetRecentOffersAsync(limit);

            var response = offers.Select(o => new OfferResponse(
                o.Id,
                o.PlatformId,
                o.Platform?.Name,
                o.Title,
                o.Price,
                o.Currency,
                o.Url,
                o.ImageUrl,
                o.SellerName,
                o.Location,
                o.AdditionalInfo,
                o.CreatedAt,
                o.Status,
                o.VehicleDetail != null ? new VehicleDetailResponse(
                    o.VehicleDetail.OfferId,
                    o.VehicleDetail.ProductionYear,
                    o.VehicleDetail.Mileage,
                    o.VehicleDetail.FuelType,
                    o.VehicleDetail.Gearbox,
                    o.VehicleDetail.EnginePower,
                    o.VehicleDetail.BodyType
                ) : null
            ));
        }

        return (recordsCount, offers);
    }

    private static int ParseRecordsCount(string output)
    {
        var regex = new Regex(@"(?<=.*: )\d+");
        var match = regex.Match(output);
        return match.Success ? int.Parse(match.Value) : 0;
    }
}
