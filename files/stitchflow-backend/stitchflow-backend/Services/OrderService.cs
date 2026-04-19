using Microsoft.EntityFrameworkCore;
using StitchFlow.API.Data;
using StitchFlow.API.DTOs;
using StitchFlow.API.Models;

namespace StitchFlow.API.Services;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(Guid tailorUserId, CreateOrderRequest request);
    Task<OrderDto> UpdateOrderAsync(Guid orderId, Guid updatedByUserId, UpdateOrderRequest request);
    Task<OrderDto> GetOrderByIdAsync(Guid orderId);
    Task<OrderDto> GetOrderByNumberAsync(string orderNumber);
    Task<PagedResult<OrderDto>> GetTailorOrdersAsync(Guid tailorProfileId, string? status, int page, int pageSize);
    Task<PagedResult<OrderDto>> GetCustomerOrdersAsync(Guid customerId, int page, int pageSize);
    Task UploadClothImageAsync(Guid orderId, string imageUrl);
}

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifService;

    public OrderService(AppDbContext db, INotificationService notifService)
    {
        _db          = db;
        _notifService = notifService;
    }

    // ── CREATE ORDER ─────────────────────────────────────────────
    public async Task<OrderDto> CreateOrderAsync(Guid tailorUserId, CreateOrderRequest req)
    {
        var tailorProfile = await _db.TailorProfiles
            .FirstOrDefaultAsync(t => t.UserId == tailorUserId)
            ?? throw new KeyNotFoundException("Tailor profile not found.");

        var customer = await _db.Users.FindAsync(req.CustomerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        // Generate order number
        var count       = await _db.Orders.CountAsync();
        var orderNumber = $"SF-{(count + 1000):D4}";

        var order = new Order
        {
            OrderNumber          = orderNumber,
            CustomerId           = req.CustomerId,
            TailorId             = tailorProfile.Id,
            DesignId             = req.DesignId,
            MeasurementId        = req.MeasurementId,
            GarmentType          = req.GarmentType,
            SpecialInstructions  = req.SpecialInstructions,
            DeliveryDate         = req.DeliveryDate,
            Priority             = req.Priority,
            TotalAmount          = req.TotalAmount,
            AdvancePaid          = req.AdvancePaid,
            Status               = "pending"
        };

        _db.Orders.Add(order);

        // Log initial status
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId   = order.Id,
            NewStatus = "pending",
            Note      = "Order created",
            ChangedBy = tailorUserId
        });

        await _db.SaveChangesAsync();

        // Notify customer
        await _notifService.SendAsync(new Notification
        {
            UserId  = req.CustomerId,
            OrderId = order.Id,
            Title   = $"New Order #{orderNumber} Created",
            Message = $"Your {req.GarmentType} order has been created at {tailorProfile.ShopName}. Expected delivery: {req.DeliveryDate?.ToString("MMM dd, yyyy") ?? "TBD"}.",
            Type    = "order_update"
        });

        return await GetOrderByIdAsync(order.Id);
    }

    // ── UPDATE ORDER ─────────────────────────────────────────────
    public async Task<OrderDto> UpdateOrderAsync(Guid orderId, Guid updatedByUserId, UpdateOrderRequest req)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Tailor)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Order not found.");

        var oldStatus = order.Status;

        if (req.Status != null) order.Status = req.Status;
        if (req.TailorNote != null) order.TailorNote = req.TailorNote;
        if (req.DeliveryDate.HasValue) order.DeliveryDate = req.DeliveryDate;
        if (req.TotalAmount.HasValue) order.TotalAmount = req.TotalAmount.Value;
        if (req.AdvancePaid.HasValue) order.AdvancePaid = req.AdvancePaid.Value;

        // Log status change
        if (req.Status != null && req.Status != oldStatus)
        {
            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId   = order.Id,
                OldStatus = oldStatus,
                NewStatus = req.Status,
                Note      = req.TailorNote,
                ChangedBy = updatedByUserId
            });

            // Notify customer if status changed
            if (req.NotifyCustomer)
            {
                var statusMessage = req.Status switch
                {
                    "cutting"    => $"Your {order.GarmentType} is being cut now.",
                    "stitching"  => $"Your {order.GarmentType} stitching has started!",
                    "finishing"  => $"Your {order.GarmentType} is in final finishing stage.",
                    "ready"      => $"Great news! Your {order.GarmentType} is ready for pickup/delivery! 🎉",
                    "delivered"  => $"Your {order.GarmentType} has been delivered. Thank you!",
                    _ => $"Your order #{order.OrderNumber} status is now: {req.Status}."
                };

                await _notifService.SendAsync(new Notification
                {
                    UserId  = order.CustomerId,
                    OrderId = order.Id,
                    Title   = $"Order #{order.OrderNumber} — {req.Status.Replace("_", " ").ToTitleCase()}",
                    Message = statusMessage + (req.TailorNote != null ? $"\n\nTailor note: {req.TailorNote}" : ""),
                    Type    = "order_update"
                });
            }
        }

        await _db.SaveChangesAsync();
        return await GetOrderByIdAsync(order.Id);
    }

    // ── GET ORDER BY ID ───────────────────────────────────────────
    public async Task<OrderDto> GetOrderByIdAsync(Guid orderId)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Tailor)
            .Include(o => o.Design).ThenInclude(d => d!.Images)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Order not found.");

        return MapToDto(order);
    }

    // ── GET ORDER BY NUMBER ───────────────────────────────────────
    public async Task<OrderDto> GetOrderByNumberAsync(string orderNumber)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Tailor)
            .Include(o => o.Design).ThenInclude(d => d!.Images)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber)
            ?? throw new KeyNotFoundException($"Order {orderNumber} not found.");

        return MapToDto(order);
    }

    // ── TAILOR ORDERS ─────────────────────────────────────────────
    public async Task<PagedResult<OrderDto>> GetTailorOrdersAsync(
        Guid tailorProfileId, string? status, int page, int pageSize)
    {
        var query = _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Tailor)
            .Include(o => o.Design)
            .Include(o => o.StatusHistory)
            .Where(o => o.TailorId == tailorProfileId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<OrderDto>
        {
            Items      = items.Select(MapToDto).ToList(),
            Page       = page,
            PageSize   = pageSize,
            TotalCount = total
        };
    }

    // ── CUSTOMER ORDERS ───────────────────────────────────────────
    public async Task<PagedResult<OrderDto>> GetCustomerOrdersAsync(
        Guid customerId, int page, int pageSize)
    {
        var query = _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Tailor)
            .Include(o => o.Design)
            .Include(o => o.StatusHistory)
            .Where(o => o.CustomerId == customerId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<OrderDto>
        {
            Items      = items.Select(MapToDto).ToList(),
            Page       = page,
            PageSize   = pageSize,
            TotalCount = total
        };
    }

    // ── UPLOAD CLOTH IMAGE ────────────────────────────────────────
    public async Task UploadClothImageAsync(Guid orderId, string imageUrl)
    {
        var order = await _db.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException("Order not found.");

        order.ClothImageUrl = imageUrl;
        await _db.SaveChangesAsync();
    }

    // ── MAP ───────────────────────────────────────────────────────
    private static OrderDto MapToDto(Order o) => new()
    {
        Id                  = o.Id,
        OrderNumber         = o.OrderNumber,
        CustomerId          = o.CustomerId,
        CustomerName        = o.Customer.Name,
        CustomerPhone       = o.Customer.Phone,
        TailorId            = o.TailorId,
        TailorShopName      = o.Tailor.ShopName,
        GarmentType         = o.GarmentType,
        ClothImageUrl       = o.ClothImageUrl,
        SpecialInstructions = o.SpecialInstructions,
        DeliveryDate        = o.DeliveryDate,
        Priority            = o.Priority,
        Status              = o.Status,
        TailorNote          = o.TailorNote,
        TotalAmount         = o.TotalAmount,
        AdvancePaid         = o.AdvancePaid,
        CreatedAt           = o.CreatedAt,
        UpdatedAt           = o.UpdatedAt,
        Design = o.Design == null ? null : new DesignDto
        {
            Id           = o.Design.Id,
            Name         = o.Design.Name,
            GarmentType  = o.Design.GarmentType,
            Category     = o.Design.Category,
            BasePrice    = o.Design.BasePrice,
            ImageUrls    = o.Design.Images.Select(i => i.ImageUrl).ToList(),
            PrimaryImageUrl = o.Design.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
        },
        StatusHistory = o.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => new OrderStatusHistoryDto
            {
                OldStatus = h.OldStatus,
                NewStatus = h.NewStatus,
                Note      = h.Note,
                ChangedAt = h.ChangedAt
            }).ToList()
    };
}

// Helper extension
public static class StringExtensions
{
    public static string ToTitleCase(this string s) =>
        string.IsNullOrEmpty(s) ? s :
        char.ToUpper(s[0]) + s[1..];
}
