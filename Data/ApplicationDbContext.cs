using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<HostApplication> HostApplications => Set<HostApplication>();
    public DbSet<HostProperty> HostProperties => Set<HostProperty>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<GuestDocument> GuestDocuments => Set<GuestDocument>();
    public DbSet<CheckInProcess> CheckInProcesses => Set<CheckInProcess>();
    public DbSet<UserMessage> UserMessages => Set<UserMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<PropertyAmenity> PropertyAmenities => Set<PropertyAmenity>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<HostApplication>().HasOne(x => x.Applicant).WithMany(x => x.HostApplications)
            .HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HostProperty>().HasOne(x => x.Host).WithMany(x => x.HostedProperties)
            .HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Booking>().HasOne(x => x.Guest).WithMany(x => x.GuestBookings)
            .HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<UserMessage>().HasOne(x => x.Sender).WithMany(x => x.SentMessages)
            .HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<UserMessage>().HasOne(x => x.Recipient).WithMany(x => x.ReceivedMessages)
            .HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Notification>().HasOne(x => x.User).WithMany(x => x.Notifications)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Review>().HasOne(x => x.Reviewer).WithMany()
            .HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<WishlistItem>().HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<WishlistItem>().HasIndex(x => new { x.UserId, x.PropertyId }).IsUnique();
        builder.Entity<CheckInProcess>().HasIndex(x => x.PropertyId).IsUnique();
        builder.Entity<Payment>().HasIndex(x => x.BookingId).IsUnique();
        builder.Entity<PropertyAmenity>().HasKey(x => new { x.PropertyId, x.AmenityId });
        builder.Entity<PropertyAmenity>().HasOne(x => x.Property).WithMany(x => x.PropertyAmenities)
            .HasForeignKey(x => x.PropertyId);
        builder.Entity<PropertyAmenity>().HasOne(x => x.Amenity).WithMany(x => x.PropertyAmenities)
            .HasForeignKey(x => x.AmenityId);

        foreach (var property in builder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }
    }
}
