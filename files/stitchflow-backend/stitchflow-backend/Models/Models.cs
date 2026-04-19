using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StitchFlow.API.Models;

// ─────────────────────────────────────────────
//  User
// ─────────────────────────────────────────────
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Role { get; set; } = string.Empty;   // "tailor" | "customer"

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public TailorProfile? TailorProfile { get; set; }
    public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

// ─────────────────────────────────────────────
//  TailorProfile
// ─────────────────────────────────────────────
public class TailorProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    [Required, MaxLength(200)]
    public string ShopName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Specialization { get; set; } = string.Empty;  // gents | ladies | kids | all

    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public string? LogoUrl { get; set; }

    [Required, MaxLength(20)]
    public string ShopCode { get; set; } = string.Empty;

    public string? Bio { get; set; }
    public bool IsVerified { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Design> Designs { get; set; } = new List<Design>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<CustomerTailorLink> CustomerLinks { get; set; } = new List<CustomerTailorLink>();
}

// ─────────────────────────────────────────────
//  CustomerTailorLink
// ─────────────────────────────────────────────
public class CustomerTailorLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid TailorId { get; set; }
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    public User Customer { get; set; } = null!;
    public TailorProfile Tailor { get; set; } = null!;
}

// ─────────────────────────────────────────────
//  Measurement
// ─────────────────────────────────────────────
public class Measurement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid TailorId { get; set; }

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Customer { get; set; } = null!;
    public TailorProfile Tailor { get; set; } = null!;
}

// ─────────────────────────────────────────────
//  Design
// ─────────────────────────────────────────────
public class Design
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TailorId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, MaxLength(20)]
    public string Category { get; set; } = string.Empty;  // men | women | kids | unisex

    [Required, MaxLength(50)]
    public string GarmentType { get; set; } = string.Empty;  // Shalwar Kameez | Sherwani | etc.

    [Column(TypeName = "decimal(10,2)")]
    public decimal BasePrice { get; set; } = 0;

    public int StitchingDays { get; set; } = 7;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public TailorProfile Tailor { get; set; } = null!;
    public ICollection<DesignImage> Images { get; set; } = new List<DesignImage>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

// ─────────────────────────────────────────────
//  DesignImage
// ─────────────────────────────────────────────
public class DesignImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DesignId { get; set; }

    [Required]
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsPrimary { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Design Design { get; set; } = null!;
}

// ─────────────────────────────────────────────
//  Order
// ─────────────────────────────────────────────
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(20)]
    public string OrderNumber { get; set; } = string.Empty;  // SF-1031

    public Guid CustomerId { get; set; }
    public Guid TailorId { get; set; }
    public Guid? DesignId { get; set; }
    public Guid? MeasurementId { get; set; }

    [Required, MaxLength(50)]
    public string GarmentType { get; set; } = string.Empty;

    public string? ClothImageUrl { get; set; }
    public string? SpecialInstructions { get; set; }
    public DateOnly? DeliveryDate { get; set; }

    [MaxLength(20)]
    public string Priority { get; set; } = "normal";  // normal | urgent | rush

    [MaxLength(30)]
    public string Status { get; set; } = "pending";
    // pending | in_progress | cutting | stitching | finishing | ready | delivered | cancelled

    public string? TailorNote { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalAmount { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal AdvancePaid { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Customer { get; set; } = null!;
    public TailorProfile Tailor { get; set; } = null!;
    public Design? Design { get; set; }
    public Measurement? Measurement { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public Review? Review { get; set; }
}

// ─────────────────────────────────────────────
//  OrderStatusHistory
// ─────────────────────────────────────────────
public class OrderStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}

// ─────────────────────────────────────────────
//  Payment
// ─────────────────────────────────────────────
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [Required, MaxLength(30)]
    public string PaymentMethod { get; set; } = string.Empty;  // cash | bank_transfer | online | mobile_wallet

    [MaxLength(20)]
    public string Status { get; set; } = "pending";  // pending | paid | failed | refunded

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;
}

// ─────────────────────────────────────────────
//  Notification
// ─────────────────────────────────────────────
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? OrderId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Type { get; set; } = string.Empty;  // order_update | payment | measurement | system | review

    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Order? Order { get; set; }
}

// ─────────────────────────────────────────────
//  Review
// ─────────────────────────────────────────────
public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid TailorId { get; set; }
    public int Rating { get; set; }  // 1–5
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;
    public User Customer { get; set; } = null!;
    public TailorProfile Tailor { get; set; } = null!;
}

// ─────────────────────────────────────────────
//  RefreshToken
// ─────────────────────────────────────────────
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
