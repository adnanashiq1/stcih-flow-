using System.ComponentModel.DataAnnotations;

namespace StitchFlow.API.DTOs;

// ════════════════════════════════════════
//  AUTH
// ════════════════════════════════════════
public class RegisterTailorRequest
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    [Required] public string ShopName { get; set; } = string.Empty;
    [Required] public string Specialization { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Bio { get; set; }
}

public class RegisterCustomerRequest
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    public string? ShopCode { get; set; }  // to link to a tailor on registration
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class RefreshTokenRequest
{
    [Required] public string RefreshToken { get; set; } = string.Empty;
}

// ════════════════════════════════════════
//  USER / PROFILE
// ════════════════════════════════════════
public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public TailorProfileDto? TailorProfile { get; set; }
}

public class TailorProfileDto
{
    public Guid Id { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? LogoUrl { get; set; }
    public string ShopCode { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public bool IsVerified { get; set; }
    public double? AverageRating { get; set; }
    public int TotalReviews { get; set; }
}

public class UpdateTailorProfileRequest
{
    public string? ShopName { get; set; }
    public string? Specialization { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Bio { get; set; }
}

// ════════════════════════════════════════
//  MEASUREMENTS
// ════════════════════════════════════════
public class CreateMeasurementRequest
{
    [Required] public Guid CustomerId { get; set; }
    public decimal? Neck { get; set; }
    public decimal? Chest { get; set; }
    public decimal? Waist { get; set; }
    public decimal? Shoulder { get; set; }
    public decimal? Sleeve { get; set; }
    public decimal? Length { get; set; }
    public decimal? Hip { get; set; }
    public decimal? Thigh { get; set; }
    public decimal? Inseam { get; set; }
    public string? Notes { get; set; }
}

public class MeasurementDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal? Neck { get; set; }
    public decimal? Chest { get; set; }
    public decimal? Waist { get; set; }
    public decimal? Shoulder { get; set; }
    public decimal? Sleeve { get; set; }
    public decimal? Length { get; set; }
    public decimal? Hip { get; set; }
    public decimal? Thigh { get; set; }
    public decimal? Inseam { get; set; }
    public string? Notes { get; set; }
    public string? ReferenceImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ════════════════════════════════════════
//  DESIGNS
// ════════════════════════════════════════
public class CreateDesignRequest
{
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required] public string Category { get; set; } = string.Empty;
    [Required] public string GarmentType { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public int StitchingDays { get; set; } = 7;
    public string? PrimaryImageUrl { get; set; }  // ← ADD THIS LINE
}

public class UpdateDesignRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? GarmentType { get; set; }
    public decimal? BasePrice { get; set; }
    public int? StitchingDays { get; set; }
    public bool? IsActive { get; set; }
    public string? PrimaryImageUrl { get; set; }  // ← ADD THIS LINE
}

public class DesignDto
{
    public Guid Id { get; set; }
    public Guid TailorId { get; set; }
    public string TailorShopName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string GarmentType { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public int StitchingDays { get; set; }
    public bool IsActive { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public string? PrimaryImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ════════════════════════════════════════
//  ORDERS
// ════════════════════════════════════════
public class CreateOrderRequest
{
    [Required] public Guid CustomerId { get; set; }
    [Required] public string GarmentType { get; set; } = string.Empty;
    public Guid? DesignId { get; set; }
    public Guid? MeasurementId { get; set; }
    public string? SpecialInstructions { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string Priority { get; set; } = "normal";
    public decimal TotalAmount { get; set; }
    public decimal AdvancePaid { get; set; }
}

public class UpdateOrderRequest
{
    public string? Status { get; set; }
    public string? TailorNote { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? AdvancePaid { get; set; }
    public bool NotifyCustomer { get; set; } = true;
}

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public Guid TailorId { get; set; }
    public string TailorShopName { get; set; } = string.Empty;
    public DesignDto? Design { get; set; }
    public string GarmentType { get; set; } = string.Empty;
    public string? ClothImageUrl { get; set; }
    public string? SpecialInstructions { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TailorNote { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AdvancePaid { get; set; }
    public decimal BalanceDue => TotalAmount - AdvancePaid;
    public List<OrderStatusHistoryDto> StatusHistory { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OrderStatusHistoryDto
{
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}

// ════════════════════════════════════════
//  PAYMENTS
// ════════════════════════════════════════
public class CreatePaymentRequest
{
    [Required] public Guid OrderId { get; set; }
    [Required] public decimal Amount { get; set; }
    [Required] public string PaymentMethod { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime PaymentDate { get; set; }
}

// ════════════════════════════════════════
//  NOTIFICATIONS
// ════════════════════════════════════════
public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ════════════════════════════════════════
//  REVIEWS
// ════════════════════════════════════════
public class CreateReviewRequest
{
    [Required] public Guid OrderId { get; set; }
    [Required, Range(1, 5)] public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class ReviewDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ════════════════════════════════════════
//  CUSTOMERS (Tailor's view)
// ════════════════════════════════════════
public class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int TotalOrders { get; set; }
    public MeasurementDto? LatestMeasurement { get; set; }
    public DateTime LinkedAt { get; set; }
}

// ════════════════════════════════════════
//  ANALYTICS
// ════════════════════════════════════════
public class TailorAnalyticsDto
{
    public decimal TotalRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int TotalCustomers { get; set; }
    public double RepeatCustomerPercent { get; set; }
    public double AverageRating { get; set; }
    public List<MonthlyRevenueDto> MonthlyBreakdown { get; set; } = new();
    public List<GarmentTypeCountDto> GarmentTypeCounts { get; set; } = new();
    public List<TopDesignDto> TopDesigns { get; set; } = new();
}

public class MonthlyRevenueDto
{
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public class GarmentTypeCountDto
{
    public string GarmentType { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TopDesignDto
{
    public Guid DesignId { get; set; }
    public string DesignName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
}

// ════════════════════════════════════════
//  PAGINATION
// ════════════════════════════════════════
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

// ════════════════════════════════════════
//  API RESPONSE WRAPPER
// ════════════════════════════════════════
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string error) =>
        new() { Success = false, Errors = new List<string> { error } };
}
