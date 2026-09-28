using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public static class Roles
{
    public const string Guest = "Guest";
    public const string Host = "Host";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
}

public enum HostApplicationStatus { Pending, Approved, Rejected }
public enum BookingStatus { Pending, Approved, Rejected, CheckedIn, Cancelled }
public enum GuestDocumentStatus { Pending, Verified, Rejected }
public enum DocumentType { IdentityDocument, Passport }
public enum PaymentStatus { Pending, Paid, Refunded }
public enum NotificationType { General, Application, Booking, Message, CheckIn }

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(80)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(80)] public string LastName { get; set; } = string.Empty;
    [StringLength(500)] public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HostApplication> HostApplications { get; set; } = new List<HostApplication>();
    public ICollection<HostProperty> HostedProperties { get; set; } = new List<HostProperty>();
    public ICollection<Booking> GuestBookings { get; set; } = new List<Booking>();
    public ICollection<UserMessage> SentMessages { get; set; } = new List<UserMessage>();
    public ICollection<UserMessage> ReceivedMessages { get; set; } = new List<UserMessage>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

public class HostApplication
{
    public int Id { get; set; }
    [Required] public string ApplicantId { get; set; } = string.Empty;
    public ApplicationUser Applicant { get; set; } = null!;
    [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(3000)] public string Description { get; set; } = string.Empty;
    [Range(1, 1_000_000)] public decimal PricePerNight { get; set; }
    [Range(0, 1_000_000)] public decimal CleaningFee { get; set; }
    [Range(0, 1_000_000)] public decimal ServiceFee { get; set; }
    [Required, StringLength(250)] public string Address { get; set; } = string.Empty;
    [Required, StringLength(100)] public string City { get; set; } = string.Empty;
    [Required, StringLength(80)] public string PropertyType { get; set; } = string.Empty;
    public HostApplicationStatus Status { get; set; } = HostApplicationStatus.Pending;
    [StringLength(1000)] public string? AdminComment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public ICollection<PropertyImage> PropertyImages { get; set; } = new List<PropertyImage>();
}

public class HostProperty
{
    public int Id { get; set; }
    [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(3000)] public string Description { get; set; } = string.Empty;
    [Range(1, 1_000_000)] public decimal PricePerNight { get; set; }
    [Range(0, 1_000_000)] public decimal CleaningFee { get; set; }
    [Range(0, 1_000_000)] public decimal ServiceFee { get; set; }
    [Required] public string HostId { get; set; } = string.Empty;
    public ApplicationUser Host { get; set; } = null!;
    [Required, StringLength(250)] public string Address { get; set; } = string.Empty;
    [Required, StringLength(100)] public string City { get; set; } = string.Empty;
    [Required, StringLength(80)] public string PropertyType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PropertyImage> PropertyImages { get; set; } = new List<PropertyImage>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<PropertyAmenity> PropertyAmenities { get; set; } = new List<PropertyAmenity>();
    public CheckInProcess? CheckInProcess { get; set; }
}

public class PropertyImage
{
    public int Id { get; set; }
    public int? HostPropertyId { get; set; }
    public HostProperty? HostProperty { get; set; }
    public int? HostApplicationId { get; set; }
    public HostApplication? HostApplication { get; set; }
    [Required, StringLength(500)] public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Booking
{
    public int Id { get; set; }
    [Required] public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;
    [Required] public string GuestId { get; set; } = string.Empty;
    public ApplicationUser Guest { get; set; } = null!;
    [DataType(DataType.Date)] public DateTime CheckInDate { get; set; }
    [DataType(DataType.Date)] public DateTime CheckOutDate { get; set; }
    [Range(1, 30)] public int TravelerCount { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<GuestDocument> GuestDocuments { get; set; } = new List<GuestDocument>();
    public Payment? Payment { get; set; }
}

public class GuestDocument
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public DocumentType DocumentType { get; set; }
    [Required, StringLength(500)] public string DocumentUrl { get; set; } = string.Empty;
    public GuestDocumentStatus Status { get; set; } = GuestDocumentStatus.Pending;
    [StringLength(500)] public string? HostComment { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public class CheckInProcess
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;
    [Required, StringLength(2000)] public string Instructions { get; set; } = string.Empty;
    [Required, StringLength(250)] public string RequiredDocuments { get; set; } = "ID or Passport";
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class UserMessage
{
    public int Id { get; set; }
    [Required] public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;
    [Required] public string RecipientId { get; set; } = string.Empty;
    public ApplicationUser Recipient { get; set; } = null!;
    [Required, StringLength(160)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Message { get; set; } = string.Empty;
    [StringLength(300)] public string? Link { get; set; }
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ActivityLog
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    [StringLength(100)] public string? UserEmail { get; set; }
    [Required, StringLength(200)] public string Action { get; set; } = string.Empty;
    [StringLength(2000)] public string? Details { get; set; }
    [StringLength(60)] public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    [Range(0, 1_000_000)] public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Review
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;
    [Required] public string ReviewerId { get; set; } = string.Empty;
    public ApplicationUser Reviewer { get; set; } = null!;
    [Range(1, 5)] public int Rating { get; set; }
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Amenity
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(500)] public string? IconUrl { get; set; }
    public ICollection<PropertyAmenity> PropertyAmenities { get; set; } = new List<PropertyAmenity>();
}

public class PropertyAmenity
{
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;
    public int AmenityId { get; set; }
    public Amenity Amenity { get; set; } = null!;
}

public class WishlistItem
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
