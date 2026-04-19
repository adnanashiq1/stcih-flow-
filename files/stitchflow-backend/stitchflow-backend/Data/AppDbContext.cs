using Microsoft.EntityFrameworkCore;
using StitchFlow.API.Models;
using System.Linq;

namespace StitchFlow.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User>                 Users                { get; set; }
    public DbSet<TailorProfile>        TailorProfiles       { get; set; }
    public DbSet<CustomerTailorLink>   CustomerTailorLinks  { get; set; }
    public DbSet<Measurement>          Measurements         { get; set; }
    public DbSet<Design>               Designs              { get; set; }
    public DbSet<DesignImage>          DesignImages         { get; set; }
    public DbSet<Order>                Orders               { get; set; }
    public DbSet<OrderStatusHistory>   OrderStatusHistories { get; set; }
    public DbSet<Payment>              Payments             { get; set; }
    public DbSet<Notification>         Notifications        { get; set; }
    public DbSet<Review>               Reviews              { get; set; }
    public DbSet<RefreshToken>         RefreshTokens        { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── 1. Force snake_case columns (Fixes the shop_code error) ──
        foreach (var entity in mb.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                // Converts "ShopCode" -> "shop_code", "Email" -> "email"
                var columnName = string.Concat(property.Name.Select((x, i) => 
                    i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
                
                property.SetColumnName(columnName);
            }
        }

        // ── 2. snake_case table names ──
        mb.Entity<User>().ToTable("users");
        mb.Entity<TailorProfile>().ToTable("tailor_profiles");
        mb.Entity<CustomerTailorLink>().ToTable("customer_tailor_links");
        mb.Entity<Measurement>().ToTable("measurements");
        mb.Entity<Design>().ToTable("designs");
        mb.Entity<DesignImage>().ToTable("design_images");
        mb.Entity<Order>().ToTable("orders");
        mb.Entity<OrderStatusHistory>().ToTable("order_status_history");
        mb.Entity<Payment>().ToTable("payments");
        mb.Entity<Notification>().ToTable("notifications");
        mb.Entity<Review>().ToTable("reviews");
        mb.Entity<RefreshToken>().ToTable("refresh_tokens");

        // ── 3. Unique constraints ──
        mb.Entity<User>().HasIndex(u => u.Email).IsUnique();
        mb.Entity<TailorProfile>().HasIndex(t => t.ShopCode).IsUnique();
        mb.Entity<Order>().HasIndex(o => o.OrderNumber).IsUnique();
        mb.Entity<CustomerTailorLink>()
            .HasIndex(l => new { l.CustomerId, l.TailorId }).IsUnique();
        mb.Entity<Review>()
            .HasIndex(r => new { r.OrderId, r.CustomerId }).IsUnique();

        // ── 4. Relationships ──
        mb.Entity<TailorProfile>()
            .HasOne(t => t.User)
            .WithOne(u => u.TailorProfile)
            .HasForeignKey<TailorProfile>(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Order>()
            .HasOne(o => o.Tailor)
            .WithMany(t => t.Orders)
            .HasForeignKey(o => o.TailorId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<OrderStatusHistory>()
            .HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Review>()
            .HasOne(r => r.Order)
            .WithOne(o => o.Review)
            .HasForeignKey<Review>(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(ct);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is User u)
            {
                if (entry.State == EntityState.Added) u.CreatedAt = DateTime.UtcNow;
                u.UpdatedAt = DateTime.UtcNow;
            }
            if (entry.Entity is TailorProfile tp)
            {
                if (entry.State == EntityState.Added) tp.CreatedAt = DateTime.UtcNow;
                tp.UpdatedAt = DateTime.UtcNow;
            }
            if (entry.Entity is Order o)
            {
                if (entry.State == EntityState.Added) o.CreatedAt = DateTime.UtcNow;
                o.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}