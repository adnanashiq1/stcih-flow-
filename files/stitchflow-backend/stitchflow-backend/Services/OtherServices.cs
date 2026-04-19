using Microsoft.EntityFrameworkCore;
using StitchFlow.API.Data;
using StitchFlow.API.DTOs;
using StitchFlow.API.Models;

namespace StitchFlow.API.Services;

// ════════════════════════════════════════
//  NOTIFICATION SERVICE
// ════════════════════════════════════════
public interface INotificationService
{
    Task SendAsync(Notification notification);
    Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false);
    Task MarkAsReadAsync(Guid notificationId, Guid userId);
    Task MarkAllAsReadAsync(Guid userId);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db) => _db = db;

    public async Task SendAsync(Notification notification)
    {
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        // TODO: integrate SendGrid / Twilio SMS here for real-time notifications
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false)
    {
        var query = _db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Id        = n.Id,
                OrderId   = n.OrderId,
                Title     = n.Title,
                Message   = n.Message,
                Type      = n.Type,
                IsRead    = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToListAsync();
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notif = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notif != null) { notif.IsRead = true; await _db.SaveChangesAsync(); }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}

// ════════════════════════════════════════
//  MEASUREMENT SERVICE
// ════════════════════════════════════════
public interface IMeasurementService
{
    Task<MeasurementDto> CreateOrUpdateAsync(Guid tailorUserId, CreateMeasurementRequest req);
    Task<List<MeasurementDto>> GetCustomerHistoryAsync(Guid customerId, Guid tailorProfileId);
    Task<MeasurementDto?> GetLatestAsync(Guid customerId, Guid tailorProfileId);
}

public class MeasurementService : IMeasurementService
{
    private readonly AppDbContext _db;

    public MeasurementService(AppDbContext db) => _db = db;

    public async Task<MeasurementDto> CreateOrUpdateAsync(Guid tailorUserId, CreateMeasurementRequest req)
    {
        var tailorProfile = await _db.TailorProfiles
            .FirstOrDefaultAsync(t => t.UserId == tailorUserId)
            ?? throw new KeyNotFoundException("Tailor profile not found.");

        var customer = await _db.Users.FindAsync(req.CustomerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        // Always create a new measurement record (preserves history)
        var measurement = new Measurement
        {
            CustomerId = req.CustomerId,
            TailorId   = tailorProfile.Id,
            Neck       = req.Neck,
            Chest      = req.Chest,
            Waist      = req.Waist,
            Shoulder   = req.Shoulder,
            Sleeve     = req.Sleeve,
            Length     = req.Length,
            Hip        = req.Hip,
            Thigh      = req.Thigh,
            Inseam     = req.Inseam,
            Notes      = req.Notes
        };

        _db.Measurements.Add(measurement);
        await _db.SaveChangesAsync();

        return MapToDto(measurement, customer.Name);
    }

    public async Task<List<MeasurementDto>> GetCustomerHistoryAsync(Guid customerId, Guid tailorProfileId)
    {
        var customer = await _db.Users.FindAsync(customerId);
        return await _db.Measurements
            .Where(m => m.CustomerId == customerId && m.TailorId == tailorProfileId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => MapToDto(m, customer!.Name))
            .ToListAsync();
    }

    public async Task<MeasurementDto?> GetLatestAsync(Guid customerId, Guid tailorProfileId)
    {
        var customer = await _db.Users.FindAsync(customerId);
        var m = await _db.Measurements
            .Where(m => m.CustomerId == customerId && m.TailorId == tailorProfileId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        return m == null ? null : MapToDto(m, customer!.Name);
    }

    private static MeasurementDto MapToDto(Measurement m, string customerName) => new()
    {
        Id                 = m.Id,
        CustomerId         = m.CustomerId,
        CustomerName       = customerName,
        Neck               = m.Neck,
        Chest              = m.Chest,
        Waist              = m.Waist,
        Shoulder           = m.Shoulder,
        Sleeve             = m.Sleeve,
        Length             = m.Length,
        Hip                = m.Hip,
        Thigh              = m.Thigh,
        Inseam             = m.Inseam,
        Notes              = m.Notes,
        ReferenceImageUrl  = m.ReferenceImageUrl,
        CreatedAt          = m.CreatedAt,
        UpdatedAt          = m.UpdatedAt
    };
}

// ════════════════════════════════════════
//  DESIGN SERVICE
// ════════════════════════════════════════
public interface IDesignService
{
    Task<DesignDto> CreateAsync(Guid tailorUserId, CreateDesignRequest req);
    Task<DesignDto> UpdateAsync(Guid designId, Guid tailorUserId, UpdateDesignRequest req);
    Task DeleteAsync(Guid designId, Guid tailorUserId);
    Task<PagedResult<DesignDto>> GetTailorDesignsAsync(Guid tailorProfileId, string? category, string? garmentType, int page, int pageSize);
    Task<PagedResult<DesignDto>> GetPublicDesignsAsync(string? shopCode, string? category, string? garmentType, int page, int pageSize);
    Task AddImageAsync(Guid designId, Guid tailorUserId, string imageUrl, bool isPrimary);
}

public class DesignService : IDesignService
{
    private readonly AppDbContext _db;

    public DesignService(AppDbContext db) => _db = db;

    public async Task<DesignDto> CreateAsync(Guid tailorUserId, CreateDesignRequest req)
    {
        var tailor = await _db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == tailorUserId)
            ?? throw new KeyNotFoundException("Tailor profile not found.");

        var design = new Design
        {
            TailorId      = tailor.Id,
            Name          = req.Name,
            Description   = req.Description,
            Category      = req.Category,
            GarmentType   = req.GarmentType,
            BasePrice     = req.BasePrice,
            StitchingDays = req.StitchingDays
        };

        _db.Designs.Add(design);

        // Save primary image if provided
        if (!string.IsNullOrEmpty(req.PrimaryImageUrl))
        {
            _db.DesignImages.Add(new DesignImage
            {
                DesignId  = design.Id,
                ImageUrl  = req.PrimaryImageUrl,
                IsPrimary = true,
                SortOrder = 0
            });
        }

        await _db.SaveChangesAsync();

        // Reload with images so PrimaryImageUrl is populated in response
        var saved = await _db.Designs.Include(d => d.Images).FirstAsync(d => d.Id == design.Id);
        return MapToDto(saved, tailor.ShopName);
    }

    public async Task<DesignDto> UpdateAsync(Guid designId, Guid tailorUserId, UpdateDesignRequest req)
    {
        var tailor = await _db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == tailorUserId)
            ?? throw new KeyNotFoundException("Tailor profile not found.");

        var design = await _db.Designs
            .Include(d => d.Images)
            .FirstOrDefaultAsync(d => d.Id == designId && d.TailorId == tailor.Id)
            ?? throw new KeyNotFoundException("Design not found.");

        if (req.Name != null) design.Name = req.Name;
        if (req.Description != null) design.Description = req.Description;
        if (req.Category != null) design.Category = req.Category;
        if (req.GarmentType != null) design.GarmentType = req.GarmentType;
        if (req.BasePrice.HasValue) design.BasePrice = req.BasePrice.Value;
        if (req.StitchingDays.HasValue) design.StitchingDays = req.StitchingDays.Value;
        if (req.IsActive.HasValue) design.IsActive = req.IsActive.Value;

        // Update primary image if provided
        if (!string.IsNullOrEmpty(req.PrimaryImageUrl))
        {
            // Remove old primary image
            var oldPrimary = design.Images.FirstOrDefault(i => i.IsPrimary);
            if (oldPrimary != null) _db.DesignImages.Remove(oldPrimary);

            // Add new primary image
            _db.DesignImages.Add(new DesignImage
            {
                DesignId  = design.Id,
                ImageUrl  = req.PrimaryImageUrl,
                IsPrimary = true,
                SortOrder = 0
            });
        }

        design.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapToDto(design, tailor.ShopName);
    }

    public async Task DeleteAsync(Guid designId, Guid tailorUserId)
    {
        var tailor = await _db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == tailorUserId)
            ?? throw new KeyNotFoundException("Tailor profile not found.");

        var design = await _db.Designs.FirstOrDefaultAsync(d => d.Id == designId && d.TailorId == tailor.Id)
            ?? throw new KeyNotFoundException("Design not found.");

        design.IsActive = false;  // soft delete
        await _db.SaveChangesAsync();
    }

    public async Task<PagedResult<DesignDto>> GetTailorDesignsAsync(
        Guid tailorProfileId, string? category, string? garmentType, int page, int pageSize)
    {
        var tailor = await _db.TailorProfiles.FindAsync(tailorProfileId);
        var query  = _db.Designs.Include(d => d.Images).Where(d => d.TailorId == tailorProfileId);

        if (!string.IsNullOrWhiteSpace(category))    query = query.Where(d => d.Category == category);
        if (!string.IsNullOrWhiteSpace(garmentType)) query = query.Where(d => d.GarmentType == garmentType);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PagedResult<DesignDto>
        {
            Items      = items.Select(d => MapToDto(d, tailor?.ShopName ?? "")).ToList(),
            Page       = page, PageSize = pageSize, TotalCount = total
        };
    }

    public async Task<PagedResult<DesignDto>> GetPublicDesignsAsync(
        string? shopCode, string? category, string? garmentType, int page, int pageSize)
    {
        var query = _db.Designs.Include(d => d.Images).Include(d => d.Tailor)
            .Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(shopCode))
        {
            var tailor = await _db.TailorProfiles.FirstOrDefaultAsync(t => t.ShopCode == shopCode);
            if (tailor != null) query = query.Where(d => d.TailorId == tailor.Id);
        }
        if (!string.IsNullOrWhiteSpace(category))    query = query.Where(d => d.Category == category);
        if (!string.IsNullOrWhiteSpace(garmentType)) query = query.Where(d => d.GarmentType == garmentType);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return new PagedResult<DesignDto>
        {
            Items      = items.Select(d => MapToDto(d, d.Tailor.ShopName)).ToList(),
            Page       = page, PageSize = pageSize, TotalCount = total
        };
    }

    public async Task AddImageAsync(Guid designId, Guid tailorUserId, string imageUrl, bool isPrimary)
    {
        var tailor = await _db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == tailorUserId)
            ?? throw new KeyNotFoundException("Tailor not found.");

        var design = await _db.Designs.Include(d => d.Images)
            .FirstOrDefaultAsync(d => d.Id == designId && d.TailorId == tailor.Id)
            ?? throw new KeyNotFoundException("Design not found.");

        if (isPrimary)
            foreach (var img in design.Images) img.IsPrimary = false;

        _db.DesignImages.Add(new DesignImage
        {
            DesignId  = designId,
            ImageUrl  = imageUrl,
            IsPrimary = isPrimary || !design.Images.Any(),
            SortOrder = design.Images.Count
        });

        await _db.SaveChangesAsync();
    }

    private static DesignDto MapToDto(Design d, string shopName) => new()
    {
        Id              = d.Id,
        TailorId        = d.TailorId,
        TailorShopName  = shopName,
        Name            = d.Name,
        Description     = d.Description,
        Category        = d.Category,
        GarmentType     = d.GarmentType,
        BasePrice       = d.BasePrice,
        StitchingDays   = d.StitchingDays,
        IsActive        = d.IsActive,
        ImageUrls       = d.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList(),
        PrimaryImageUrl = d.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl,
        CreatedAt       = d.CreatedAt
    };
}

// ════════════════════════════════════════
//  ANALYTICS SERVICE
// ════════════════════════════════════════
public interface IAnalyticsService
{
    Task<TailorAnalyticsDto> GetTailorAnalyticsAsync(Guid tailorUserId);
}

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db) => _db = db;

    public async Task<TailorAnalyticsDto> GetTailorAnalyticsAsync(Guid tailorUserId)
    {
        var tailor = await _db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == tailorUserId)
            ?? throw new KeyNotFoundException("Tailor profile not found.");

        var orders = await _db.Orders.Where(o => o.TailorId == tailor.Id).ToListAsync();

        var now            = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthlyOrders  = orders.Where(o => o.CreatedAt >= thisMonthStart).ToList();

        // Repeat customers: customers with more than 1 order
        var customerCounts = orders.GroupBy(o => o.CustomerId).ToList();
        var repeatCount    = customerCounts.Count(g => g.Count() > 1);
        var repeatPercent  = customerCounts.Count > 0
            ? Math.Round((double)repeatCount / customerCounts.Count * 100, 1)
            : 0;

        // Monthly breakdown (last 6 months)
        var monthlyBreakdown = Enumerable.Range(0, 6)
            .Select(i => now.AddMonths(-i))
            .Select(month =>
            {
                var start  = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var end    = start.AddMonths(1);
                var mOrds  = orders.Where(o => o.CreatedAt >= start && o.CreatedAt < end).ToList();
                return new MonthlyRevenueDto
                {
                    Month      = month.ToString("MMM yyyy"),
                    Revenue    = mOrds.Sum(o => o.TotalAmount),
                    OrderCount = mOrds.Count
                };
            })
            .Reverse()
            .ToList();

        // Top designs
        var topDesigns = await _db.Orders
            .Where(o => o.TailorId == tailor.Id && o.DesignId.HasValue)
            .GroupBy(o => o.DesignId!.Value)
            .Select(g => new { DesignId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Join(_db.Designs, x => x.DesignId, d => d.Id,
                (x, d) => new TopDesignDto { DesignId = d.Id, DesignName = d.Name, OrderCount = x.Count })
            .ToListAsync();

        // Garment type breakdown
        var garmentCounts = orders
            .GroupBy(o => o.GarmentType)
            .Select(g => new GarmentTypeCountDto { GarmentType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        // Average rating
        var ratings = await _db.Reviews.Where(r => r.TailorId == tailor.Id).Select(r => r.Rating).ToListAsync();
        var avgRating = ratings.Count > 0 ? ratings.Average() : 0;

        return new TailorAnalyticsDto
        {
            TotalRevenue           = orders.Sum(o => o.TotalAmount),
            MonthlyRevenue         = monthlyOrders.Sum(o => o.TotalAmount),
            TotalOrders            = orders.Count,
            PendingOrders          = orders.Count(o => o.Status is "pending" or "in_progress" or "cutting" or "stitching" or "finishing"),
            CompletedOrders        = orders.Count(o => o.Status is "delivered"),
            TotalCustomers         = customerCounts.Count,
            RepeatCustomerPercent  = repeatPercent,
            AverageRating          = Math.Round(avgRating, 1),
            MonthlyBreakdown       = monthlyBreakdown,
            GarmentTypeCounts      = garmentCounts,
            TopDesigns             = topDesigns
        };
    }
}