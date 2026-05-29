using Microsoft.EntityFrameworkCore;
using WyszukiwaczNet.Api.Data;
using WyszukiwaczNet.Api.Entities;

namespace WyszukiwaczNet.Api.Repositories;

public interface IOfferRepository
{
    Task<List<Offer>> GetOffersByPlatformAsync(int platformId, int limit = 50);
    Task<List<Offer>> GetNewOffersByPlatformAsync(int platformId, DateTime since, int limit = 100);
    Task<List<Offer>> GetRecentOffersAsync(int limit = 100);
    Task<Offer?> GetOfferByIdAsync(int id);
    Task<List<Platform>> GetAllPlatformsAsync();
    Task<Platform?> GetPlatformByNameAsync(string name);

    Task<List<NotificationChannel>> GetAllNotificationChannelsAsync();
    Task SaveSearchHistoryAsync(int userId, List<int> offerIds);
    Task<List<Offer>> GetSearchHistoryByUserIdAsync(int userId, int limit = 100, string? platform = null);
}

public class OfferRepository : IOfferRepository
{
    private readonly ApplicationDbContext _context;

    public OfferRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Offer>> GetOffersByPlatformAsync(int platformId, int limit = 50)
    {
        return await _context.Offers
            .Include(o => o.VehicleDetail)
            .Include(o => o.JobDetail)
            .Include(o => o.Platform)
            .Where(o => o.PlatformId == platformId && o.Status == "active")
            .OrderBy(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Offer>> GetNewOffersByPlatformAsync(int platformId, DateTime since, int limit = 100)
    {
        var offers = await _context.Offers
            .Include(o => o.VehicleDetail)
            .Include(o => o.JobDetail)
            .Include(o => o.Platform)
            .Where(o => o.PlatformId == platformId && o.Status == "active" && o.CreatedAt >= since)
            .OrderBy(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return offers;
    }

    public async Task<List<Offer>> GetRecentOffersAsync(int limit = 100)
    {
        return await _context.Offers
            .Include(o => o.VehicleDetail)
            .Include(o => o.JobDetail)
            .Include(o => o.Platform)
            .Where(o => o.Status == "active")
            .OrderBy(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Offer?> GetOfferByIdAsync(int id)
    {
        return await _context.Offers
            .Include(o => o.VehicleDetail)
            .Include(o => o.JobDetail)
            .Include(o => o.Platform)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<List<Platform>> GetAllPlatformsAsync()
    {
        return await _context.Platforms.ToListAsync();
    }

    public async Task<Platform?> GetPlatformByNameAsync(string name)
    {
        return await _context.Platforms.FirstOrDefaultAsync(p => p.Name == name);
    }

    public async Task<List<NotificationChannel>> GetAllNotificationChannelsAsync()
    {
        return await _context.NotificationChannels.ToListAsync();
    }

    public async Task SaveSearchHistoryAsync(int userId, List<int> offerIds)
    {
        var existingOfferIds = await _context.UserSearchHistories
            .Where(h => h.UserId == userId && offerIds.Contains(h.OfferId))
            .Select(h => h.OfferId)
            .ToListAsync();

        var newEntries = offerIds
            .Except(existingOfferIds)
            .Select(offerId => new UserSearchHistory { UserId = userId, OfferId = offerId });

        await _context.UserSearchHistories.AddRangeAsync(newEntries);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Offer>> GetSearchHistoryByUserIdAsync(int userId, int limit = 100, string? platform = null)
    {
        var query = _context.UserSearchHistories
            .Where(h => h.UserId == userId);

        if (!string.IsNullOrEmpty(platform))
            query = query.Where(h => h.Offer!.Platform!.Name == platform);

        var offerIds = await query
            .OrderByDescending(h => h.SearchedAt)
            .Take(limit)
            .Select(h => h.OfferId)
            .ToListAsync();

        return await _context.Offers
            .Where(o => offerIds.Contains(o.Id))
            .Include(o => o.VehicleDetail)
            .Include(o => o.JobDetail)
            .Include(o => o.Platform)
            .ToListAsync();
    }
}