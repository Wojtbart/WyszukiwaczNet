using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Repositories;

namespace WyszukiwaczNet.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class OffersController : ControllerBase
{
    private readonly IOfferRepository _offerRepository;

    public OffersController(IOfferRepository offerRepository)
    {
        _offerRepository = offerRepository;
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentOffers([FromQuery] int limit = 100)
    {
        var offers = await _offerRepository.GetRecentOffersAsync(limit);
        
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

        return Ok(new { success = true, data = response });
    }

    [HttpGet("platform/{platformName}")]
    public async Task<IActionResult> GetOffersByPlatform(string platformName, [FromQuery] int limit = 100)
    {
        var platform = await _offerRepository.GetPlatformByNameAsync(platformName);
        if (platform == null)
            return NotFound(new { success = false, message = "Nie znaleziono platformy." });

        var offers = await _offerRepository.GetOffersByPlatformAsync(platform.Id, limit);
        
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

        return Ok(new { success = true, data = response });
    }

    [HttpGet("platforms")]
    public async Task<IActionResult> GetPlatforms()
    {
        var platforms = await _offerRepository.GetAllPlatformsAsync();

        var response = platforms.Select(p => new PlatformResponse(p.Id, p.Name, p.Type));

        return Ok(new { success = true, data = response });
    }

    [HttpGet("channels")]
    public async Task<IActionResult> GetChannels()
    {
        var channels = await _offerRepository.GetAllNotificationChannelsAsync();
        var response = channels.Select(c => new { c.Id, c.Name });
        return Ok(new { success = true, data = response });
    }

    [HttpGet("history/{userId}")]
    public async Task<IActionResult> GetSearchHistory(int userId, [FromQuery] string? platform = null)
    {
        var offers = await _offerRepository.GetSearchHistoryByUserIdAsync(userId, platform: platform);
        return Ok(offers.Select(o => new OfferResponse(
            o.Id, o.PlatformId, o.Platform?.Name, o.Title, o.Price, o.Currency,
            o.Url, o.ImageUrl, o.SellerName, o.Location, o.AdditionalInfo, o.CreatedAt, o.Status,
            o.VehicleDetail != null ? new VehicleDetailResponse(
                o.VehicleDetail.OfferId, o.VehicleDetail.ProductionYear, o.VehicleDetail.Mileage,
                o.VehicleDetail.FuelType, o.VehicleDetail.Gearbox, o.VehicleDetail.EnginePower,
                o.VehicleDetail.BodyType) : null)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOfferById(int id)
    {
        var offer = await _offerRepository.GetOfferByIdAsync(id);
        
        if (offer == null)
            return NotFound(new { success = false, message = "Nie znaleziono oferty." });

        var response = new OfferResponse(
            offer.Id,
            offer.PlatformId,
            offer.Platform?.Name,
            offer.Title,
            offer.Price,
            offer.Currency,
            offer.Url,
            offer.ImageUrl,
            offer.SellerName,
            offer.Location,
            offer.AdditionalInfo,
            offer.CreatedAt,
            offer.Status,
            offer.VehicleDetail != null ? new VehicleDetailResponse(
                offer.VehicleDetail.OfferId,
                offer.VehicleDetail.ProductionYear,
                offer.VehicleDetail.Mileage,
                offer.VehicleDetail.FuelType,
                offer.VehicleDetail.Gearbox,
                offer.VehicleDetail.EnginePower,
                offer.VehicleDetail.BodyType
            ) : null
        );

        return Ok(new { success = true, data = response });
    }
}
