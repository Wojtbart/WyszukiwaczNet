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
}