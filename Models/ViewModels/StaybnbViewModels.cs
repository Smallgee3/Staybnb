using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models.ViewModels;

public class HostApplicationInputModel
{
    [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(3000), DataType(DataType.MultilineText)] public string Description { get; set; } = string.Empty;
    [Range(1, 1_000_000)] public decimal PricePerNight { get; set; }
    [Range(0, 1_000_000)] public decimal CleaningFee { get; set; }
    [Range(0, 1_000_000)] public decimal ServiceFee { get; set; }
    [Required, StringLength(250)] public string Address { get; set; } = string.Empty;
    [Required, StringLength(100)] public string City { get; set; } = string.Empty;
    [Required, StringLength(80)] public string PropertyType { get; set; } = string.Empty;
    [Required] public List<IFormFile> Images { get; set; } = new();
}

public class PropertySearchViewModel
{
    public string? City { get; set; }
    public string? PropertyType { get; set; }
    public List<HostProperty> Properties { get; set; } = new();
}

public class BookingInputModel
{
    public int PropertyId { get; set; }
    [DataType(DataType.Date), Required] public DateTime CheckInDate { get; set; } = DateTime.Today.AddDays(1);
    [DataType(DataType.Date), Required] public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(2);
    [Range(1, 30)] public int TravelerCount { get; set; } = 1;
    public HostProperty? Property { get; set; }
    public decimal CalculatedTotal { get; set; }
}

public class GuestCheckInInputModel
{
    public int BookingId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string RequiredDocuments { get; set; } = "ID or Passport";
    [Required] public DocumentType DocumentType { get; set; }
    [Required] public IFormFile? Document { get; set; }
}

public class CheckInProcessInputModel
{
    public int PropertyId { get; set; }
    [Required, StringLength(2000), DataType(DataType.MultilineText)] public string Instructions { get; set; } = string.Empty;
    [Required, StringLength(250)] public string RequiredDocuments { get; set; } = "ID or Passport";
    public bool IsEnabled { get; set; } = true;
}

public class ReviewApplicationInputModel
{
    public int Id { get; set; }
    public bool Approve { get; set; }
    [StringLength(1000), DataType(DataType.MultilineText)] public string? AdminComment { get; set; }
}

public class ComposeMessageInputModel
{
    [Required] public string RecipientId { get; set; } = string.Empty;
    [Required, StringLength(160)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(4000), DataType(DataType.MultilineText)] public string Body { get; set; } = string.Empty;
}

public class AdminDashboardViewModel
{
    public int PendingApplications { get; set; }
    public int PendingBookings { get; set; }
    public int RegisteredUsers { get; set; }
    public List<ActivityLog> RecentActivities { get; set; } = new();
}

public class HostDashboardViewModel
{
    public List<HostProperty> Properties { get; set; } = new();
    public List<Booking> PendingBookings { get; set; } = new();
    public List<GuestDocument> PendingDocuments { get; set; } = new();
}
