using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StitchFlow.API.Data;
using StitchFlow.API.DTOs;
using StitchFlow.API.Models;
using StitchFlow.API.Services;

namespace StitchFlow.API.Controllers;

// ════════════════════════════════════════
//  BASE
// ════════════════════════════════════════
[ApiController]
[Produces("application/json")]
public abstract class BaseController : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());

    protected string CurrentUserRole =>
        User.FindFirstValue(ClaimTypes.Role) ?? "";
}

// ════════════════════════════════════════
//  AUTH CONTROLLER
// ════════════════════════════════════════
[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Register a new tailor account</summary>
    [HttpPost("register/tailor")]
    public async Task<IActionResult> RegisterTailor([FromBody] RegisterTailorRequest req)
    {
        try
        {
            var result = await _auth.RegisterTailorAsync(req);
            return Ok(ApiResponse<AuthResponse>.Ok(result, "Tailor account created successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<AuthResponse>.Fail(ex.Message));
        }
    }

    /// <summary>Register a new customer account</summary>
    [HttpPost("register/customer")]
    public async Task<IActionResult> RegisterCustomer([FromBody] RegisterCustomerRequest req)
    {
        try
        {
            var result = await _auth.RegisterCustomerAsync(req);
            return Ok(ApiResponse<AuthResponse>.Ok(result, "Customer account created successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<AuthResponse>.Fail(ex.Message));
        }
    }

    /// <summary>Login with email and password</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try
        {
            var result = await _auth.LoginAsync(req);
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<AuthResponse>.Fail(ex.Message));
        }
    }

    /// <summary>Refresh access token</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest req)
    {
        try
        {
            var result = await _auth.RefreshTokenAsync(req.RefreshToken);
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<AuthResponse>.Fail(ex.Message));
        }
    }

    /// <summary>Logout (revoke refresh token)</summary>
    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest req)
    {
        await _auth.RevokeTokenAsync(req.RefreshToken);
        return Ok(ApiResponse<string>.Ok("Logged out successfully."));
    }
}

// ════════════════════════════════════════
//  ORDERS CONTROLLER
// ════════════════════════════════════════
[Route("api/orders"), Authorize]
public class OrdersController : BaseController
{
    private readonly IOrderService _orders;
    public OrdersController(IOrderService orders) => _orders = orders;

    /// <summary>Create a new order (tailor only)</summary>
    [HttpPost, Authorize(Roles = "tailor")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        try
        {
            var order = await _orders.CreateOrderAsync(CurrentUserId, req);
            return CreatedAtAction(nameof(GetById), new { id = order.Id },
                ApiResponse<OrderDto>.Ok(order, $"Order {order.OrderNumber} created."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<OrderDto>.Fail(ex.Message)); }
    }

    /// <summary>Update order status, note, delivery date (tailor only)</summary>
    [HttpPut("{id:guid}"), Authorize(Roles = "tailor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderRequest req)
    {
        try
        {
            var order = await _orders.UpdateOrderAsync(id, CurrentUserId, req);
            return Ok(ApiResponse<OrderDto>.Ok(order, "Order updated."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<OrderDto>.Fail(ex.Message)); }
    }

    /// <summary>Get order by ID</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try { return Ok(ApiResponse<OrderDto>.Ok(await _orders.GetOrderByIdAsync(id))); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<OrderDto>.Fail(ex.Message)); }
    }

    /// <summary>Track order by order number (e.g. SF-1031) — public endpoint</summary>
    [HttpGet("track/{orderNumber}"), AllowAnonymous]
    public async Task<IActionResult> Track(string orderNumber)
    {
        try { return Ok(ApiResponse<OrderDto>.Ok(await _orders.GetOrderByNumberAsync(orderNumber))); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<OrderDto>.Fail(ex.Message)); }
    }

    /// <summary>Get all orders for the logged-in tailor</summary>
    [HttpGet("tailor"), Authorize(Roles = "tailor")]
    public async Task<IActionResult> GetTailorOrders(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var profile = await db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == CurrentUserId);
        if (profile == null) return NotFound(ApiResponse<object>.Fail("Tailor profile not found."));

        var result = await _orders.GetTailorOrdersAsync(profile.Id, status, page, pageSize);
        return Ok(ApiResponse<PagedResult<OrderDto>>.Ok(result));
    }

    /// <summary>Get all orders for the logged-in customer</summary>
    [HttpGet("customer"), Authorize(Roles = "customer")]
    public async Task<IActionResult> GetCustomerOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _orders.GetCustomerOrdersAsync(CurrentUserId, page, pageSize);
        return Ok(ApiResponse<PagedResult<OrderDto>>.Ok(result));
    }
}

// ════════════════════════════════════════
//  MEASUREMENTS CONTROLLER
// ════════════════════════════════════════
[Route("api/measurements"), Authorize]
public class MeasurementsController : BaseController
{
    private readonly IMeasurementService _measurements;
    public MeasurementsController(IMeasurementService measurements) => _measurements = measurements;

    /// <summary>Add or update measurements for a customer (tailor only)</summary>
    [HttpPost, Authorize(Roles = "tailor")]
    public async Task<IActionResult> Create([FromBody] CreateMeasurementRequest req)
    {
        try
        {
            var result = await _measurements.CreateOrUpdateAsync(CurrentUserId, req);
            return Ok(ApiResponse<MeasurementDto>.Ok(result, "Measurements saved."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<MeasurementDto>.Fail(ex.Message)); }
    }

    /// <summary>Get measurement history for a customer (tailor only)</summary>
    [HttpGet("customer/{customerId:guid}"), Authorize(Roles = "tailor")]
    public async Task<IActionResult> GetHistory(Guid customerId)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var profile = await db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == CurrentUserId);
        if (profile == null) return NotFound(ApiResponse<object>.Fail("Tailor profile not found."));

        var result = await _measurements.GetCustomerHistoryAsync(customerId, profile.Id);
        return Ok(ApiResponse<List<MeasurementDto>>.Ok(result));
    }

    /// <summary>Get my own latest measurements (customer)</summary>
    [HttpGet("me"), Authorize(Roles = "customer")]
    public async Task<IActionResult> GetMyMeasurements([FromQuery] string shopCode)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var profile = await db.TailorProfiles.FirstOrDefaultAsync(t => t.ShopCode == shopCode);
        if (profile == null) return NotFound(ApiResponse<object>.Fail("Shop not found."));

        var result = await _measurements.GetLatestAsync(CurrentUserId, profile.Id);
        return Ok(ApiResponse<MeasurementDto?>.Ok(result));
    }
}

// ════════════════════════════════════════
//  DESIGNS CONTROLLER
// ════════════════════════════════════════
[Route("api/designs")]
public class DesignsController : BaseController
{
    private readonly IDesignService _designs;
    public DesignsController(IDesignService designs) => _designs = designs;

    /// <summary>Create a new design (tailor only)</summary>
    [HttpPost, Authorize(Roles = "tailor")]
    public async Task<IActionResult> Create([FromBody] CreateDesignRequest req)
    {
        try
        {
            var result = await _designs.CreateAsync(CurrentUserId, req);
            return Ok(ApiResponse<DesignDto>.Ok(result, "Design created."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<DesignDto>.Fail(ex.Message)); }
    }

    /// <summary>Update a design (tailor only)</summary>
    [HttpPut("{id:guid}"), Authorize(Roles = "tailor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDesignRequest req)
    {
        try
        {
            var result = await _designs.UpdateAsync(id, CurrentUserId, req);
            return Ok(ApiResponse<DesignDto>.Ok(result, "Design updated."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<DesignDto>.Fail(ex.Message)); }
    }

    /// <summary>Soft-delete a design (tailor only)</summary>
    [HttpDelete("{id:guid}"), Authorize(Roles = "tailor")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _designs.DeleteAsync(id, CurrentUserId);
            return Ok(ApiResponse<string>.Ok("Design removed from gallery."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<string>.Fail(ex.Message)); }
    }

    /// <summary>Get tailor's own designs with filters</summary>
    [HttpGet("tailor"), Authorize(Roles = "tailor")]
    public async Task<IActionResult> GetTailorDesigns(
        [FromQuery] string? category,
        [FromQuery] string? garmentType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var profile = await db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == CurrentUserId);
        if (profile == null) return NotFound(ApiResponse<object>.Fail("Tailor profile not found."));

        var result = await _designs.GetTailorDesignsAsync(profile.Id, category, garmentType, page, pageSize);
        return Ok(ApiResponse<PagedResult<DesignDto>>.Ok(result));
    }

    /// <summary>Browse designs publicly (by shop code, category, garment type)</summary>
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Browse(
        [FromQuery] string? shopCode,
        [FromQuery] string? category,
        [FromQuery] string? garmentType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _designs.GetPublicDesignsAsync(shopCode, category, garmentType, page, pageSize);
        return Ok(ApiResponse<PagedResult<DesignDto>>.Ok(result));
    }
}

// ════════════════════════════════════════
//  CUSTOMERS CONTROLLER (tailor's view)
// ════════════════════════════════════════
[Route("api/customers"), Authorize(Roles = "tailor")]
public class CustomersController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IMeasurementService _measurements;

    public CustomersController(AppDbContext db, IMeasurementService measurements)
    {
        _db = db; _measurements = measurements;
    }

    /// <summary>Get all customers linked to this tailor</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var profile = await _db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == CurrentUserId);
        if (profile == null) return NotFound(ApiResponse<object>.Fail("Tailor profile not found."));

        var query = _db.CustomerTailorLinks
            .Include(l => l.Customer)
            .Where(l => l.TailorId == profile.Id);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(l =>
                l.Customer.Name.Contains(search) ||
                (l.Customer.Phone != null && l.Customer.Phone.Contains(search)));

        var total = await query.CountAsync();
        var links = await query
            .OrderByDescending(l => l.LinkedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new List<CustomerDto>();
        foreach (var link in links)
        {
            var orderCount = await _db.Orders.CountAsync(o =>
                o.CustomerId == link.CustomerId && o.TailorId == profile.Id);
            var latest = await _measurements.GetLatestAsync(link.CustomerId, profile.Id);
            result.Add(new CustomerDto
            {
                Id                 = link.Customer.Id,
                Name               = link.Customer.Name,
                Email              = link.Customer.Email,
                Phone              = link.Customer.Phone,
                TotalOrders        = orderCount,
                LatestMeasurement  = latest,
                LinkedAt           = link.LinkedAt
            });
        }

        return Ok(ApiResponse<PagedResult<CustomerDto>>.Ok(new PagedResult<CustomerDto>
        {
            Items = result, Page = page, PageSize = pageSize, TotalCount = total
        }));
    }

    /// <summary>Add customer by shop code (link an existing customer to this tailor)</summary>
    [HttpPost("link/{customerId:guid}")]
    public async Task<IActionResult> LinkCustomer(Guid customerId)
    {
        var profile = await _db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == CurrentUserId);
        if (profile == null) return NotFound(ApiResponse<object>.Fail("Tailor profile not found."));

        var customer = await _db.Users.FindAsync(customerId);
        if (customer == null || customer.Role != "customer")
            return NotFound(ApiResponse<object>.Fail("Customer not found."));

        var existing = await _db.CustomerTailorLinks.AnyAsync(l =>
            l.CustomerId == customerId && l.TailorId == profile.Id);

        if (!existing)
        {
            _db.CustomerTailorLinks.Add(new CustomerTailorLink
            {
                CustomerId = customerId, TailorId = profile.Id
            });
            await _db.SaveChangesAsync();
        }

        return Ok(ApiResponse<string>.Ok("Customer linked successfully."));
    }
}

// ════════════════════════════════════════
//  PAYMENTS CONTROLLER
// ════════════════════════════════════════
[Route("api/payments"), Authorize]
public class PaymentsController : BaseController
{
    private readonly AppDbContext _db;
    public PaymentsController(AppDbContext db) => _db = db;

    /// <summary>Record a payment for an order (tailor only)</summary>
    [HttpPost, Authorize(Roles = "tailor")]
    public async Task<IActionResult> RecordPayment([FromBody] CreatePaymentRequest req)
    {
        var order = await _db.Orders.FindAsync(req.OrderId);
        if (order == null) return NotFound(ApiResponse<object>.Fail("Order not found."));

        var payment = new Payment
        {
            OrderId         = req.OrderId,
            Amount          = req.Amount,
            PaymentMethod   = req.PaymentMethod,
            Status          = "paid",
            ReferenceNumber = req.ReferenceNumber,
            Notes           = req.Notes,
            PaymentDate     = DateTime.UtcNow
        };

        _db.Payments.Add(payment);
        order.AdvancePaid += req.Amount;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Payment recorded."));
    }

    /// <summary>Get payment history for an order</summary>
    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId)
    {
        var payments = await _db.Payments
            .Include(p => p.Order).ThenInclude(o => o.Customer)
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentDto
            {
                Id              = p.Id,
                OrderId         = p.OrderId,
                OrderNumber     = p.Order.OrderNumber,
                CustomerName    = p.Order.Customer.Name,
                Amount          = p.Amount,
                PaymentMethod   = p.PaymentMethod,
                Status          = p.Status,
                ReferenceNumber = p.ReferenceNumber,
                Notes           = p.Notes,
                PaymentDate     = p.PaymentDate
            })
            .ToListAsync();

        return Ok(ApiResponse<List<PaymentDto>>.Ok(payments));
    }

    /// <summary>Get all payments for the tailor</summary>
    [HttpGet("tailor"), Authorize(Roles = "tailor")]
    public async Task<IActionResult> GetTailorPayments()
    {
        var profile = await _db.TailorProfiles.FirstOrDefaultAsync(t => t.UserId == CurrentUserId);
        if (profile == null) return NotFound(ApiResponse<object>.Fail("Tailor profile not found."));

        var payments = await _db.Payments
            .Include(p => p.Order).ThenInclude(o => o.Customer)
            .Where(p => p.Order.TailorId == profile.Id)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentDto
            {
                Id            = p.Id,
                OrderId       = p.OrderId,
                OrderNumber   = p.Order.OrderNumber,
                CustomerName  = p.Order.Customer.Name,
                Amount        = p.Amount,
                PaymentMethod = p.PaymentMethod,
                Status        = p.Status,
                PaymentDate   = p.PaymentDate
            }).ToListAsync();

        return Ok(ApiResponse<List<PaymentDto>>.Ok(payments));
    }
}

// ════════════════════════════════════════
//  NOTIFICATIONS CONTROLLER
// ════════════════════════════════════════
[Route("api/notifications"), Authorize]
public class NotificationsController : BaseController
{
    private readonly INotificationService _notif;
    public NotificationsController(INotificationService notif) => _notif = notif;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool unreadOnly = false)
    {
        var result = await _notif.GetUserNotificationsAsync(CurrentUserId, unreadOnly);
        return Ok(ApiResponse<List<NotificationDto>>.Ok(result));
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _notif.MarkAsReadAsync(id, CurrentUserId);
        return Ok(ApiResponse<string>.Ok("Marked as read."));
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notif.MarkAllAsReadAsync(CurrentUserId);
        return Ok(ApiResponse<string>.Ok("All notifications marked as read."));
    }
}

// ════════════════════════════════════════
//  ANALYTICS CONTROLLER
// ════════════════════════════════════════
[Route("api/analytics"), Authorize(Roles = "tailor")]
public class AnalyticsController : BaseController
{
    private readonly IAnalyticsService _analytics;
    public AnalyticsController(IAnalyticsService analytics) => _analytics = analytics;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var result = await _analytics.GetTailorAnalyticsAsync(CurrentUserId);
            return Ok(ApiResponse<TailorAnalyticsDto>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.Fail(ex.Message)); }
    }
}

// ════════════════════════════════════════
//  REVIEWS CONTROLLER
// ════════════════════════════════════════
[Route("api/reviews")]
public class ReviewsController : BaseController
{
    private readonly AppDbContext _db;
    public ReviewsController(AppDbContext db) => _db = db;

    /// <summary>Submit a review for a completed order (customer only)</summary>
    [HttpPost, Authorize(Roles = "customer")]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest req)
    {
        var order = await _db.Orders.FindAsync(req.OrderId);
        if (order == null) return NotFound(ApiResponse<object>.Fail("Order not found."));

        if (order.Status != "delivered")
            return BadRequest(ApiResponse<object>.Fail("Reviews can only be submitted for delivered orders."));

        var existing = await _db.Reviews.AnyAsync(r => r.OrderId == req.OrderId && r.CustomerId == CurrentUserId);
        if (existing) return Conflict(ApiResponse<object>.Fail("You already submitted a review for this order."));

        _db.Reviews.Add(new Review
        {
            OrderId    = req.OrderId,
            CustomerId = CurrentUserId,
            TailorId   = order.TailorId,
            Rating     = req.Rating,
            Comment    = req.Comment
        });
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Review submitted. Thank you!"));
    }

    /// <summary>Get all reviews for a tailor (public)</summary>
    [HttpGet("tailor/{tailorId:guid}"), AllowAnonymous]
    public async Task<IActionResult> GetTailorReviews(Guid tailorId)
    {
        var reviews = await _db.Reviews
            .Include(r => r.Customer)
            .Where(r => r.TailorId == tailorId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto
            {
                Id           = r.Id,
                CustomerName = r.Customer.Name,
                Rating       = r.Rating,
                Comment      = r.Comment,
                CreatedAt    = r.CreatedAt
            }).ToListAsync();

        return Ok(ApiResponse<List<ReviewDto>>.Ok(reviews));
    }
}
