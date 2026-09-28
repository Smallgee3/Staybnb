using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.ViewModels;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.Guest)]
public class GuestController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IFileStorageService fileStorage,
    INotificationService notifications,
    IActivityLogger logger) : Controller
{
    public async Task<IActionResult> Dashboard()
    {
        var userId = userManager.GetUserId(User)!;
        var bookings = await context.Bookings.Include(b => b.Property).ThenInclude(p => p.PropertyImages)
            .Include(b => b.GuestDocuments).Where(b => b.GuestId == userId)
            .OrderByDescending(b => b.CreatedAt).ToListAsync();
        return View(bookings);
    }

    public async Task<IActionResult> CreateBooking(int propertyId)
    {
        var property = await context.HostProperties.FirstOrDefaultAsync(p => p.Id == propertyId && p.IsActive);
        return property is null ? NotFound() : View(new BookingInputModel { PropertyId = propertyId, Property = property });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBooking(BookingInputModel input)
    {
        var property = await context.HostProperties.Include(p => p.Host).FirstOrDefaultAsync(p => p.Id == input.PropertyId && p.IsActive);
        input.Property = property;
        if (property is null) return NotFound();
        if (input.CheckInDate.Date < DateTime.Today) ModelState.AddModelError(nameof(input.CheckInDate), "Check-in cannot be in the past.");
        if (input.CheckOutDate.Date <= input.CheckInDate.Date) ModelState.AddModelError(nameof(input.CheckOutDate), "Check-out must be after check-in.");
        var conflicts = await context.Bookings.AnyAsync(b => b.PropertyId == input.PropertyId &&
            b.Status != BookingStatus.Rejected && b.Status != BookingStatus.Cancelled &&
            input.CheckInDate < b.CheckOutDate && input.CheckOutDate > b.CheckInDate);
        if (conflicts) ModelState.AddModelError(string.Empty, "These dates are no longer available for this property.");
        if (!ModelState.IsValid) return View(input);

        var nights = (input.CheckOutDate.Date - input.CheckInDate.Date).Days;
        var total = property.PricePerNight * nights + property.CleaningFee + property.ServiceFee;
        var booking = new Booking
        {
            PropertyId = property.Id,
            GuestId = userManager.GetUserId(User)!,
            CheckInDate = input.CheckInDate.Date,
            CheckOutDate = input.CheckOutDate.Date,
            TravelerCount = input.TravelerCount,
            TotalPrice = total,
            Status = BookingStatus.Pending
        };
        context.Bookings.Add(booking);
        context.Payments.Add(new Payment { Booking = booking, Amount = total, Status = PaymentStatus.Pending });
        await context.SaveChangesAsync();
        await notifications.CreateAsync(property.HostId, "New booking request", $"A guest requested {property.Title} for {nights} night(s).", "/Host/Bookings", NotificationType.Booking);
        var user = await userManager.GetUserAsync(User);
        await logger.LogAsync("Booking requested", booking.GuestId, user?.Email, $"Booking #{booking.Id}, total {total:C}", HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["Success"] = "Booking request sent. The host will review it shortly.";
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> CheckIn(int bookingId)
    {
        var booking = await GetGuestBookingAsync(bookingId);
        if (booking is null) return NotFound();
        if (booking.Status != BookingStatus.Approved)
        {
            TempData["Error"] = "Only approved bookings can proceed to check-in.";
            return RedirectToAction(nameof(Dashboard));
        }
        return View(new GuestCheckInInputModel
        {
            BookingId = booking.Id,
            PropertyTitle = booking.Property.Title,
            Instructions = booking.Property.CheckInProcess?.Instructions,
            RequiredDocuments = booking.Property.CheckInProcess?.RequiredDocuments ?? "ID or Passport"
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(GuestCheckInInputModel input)
    {
        var booking = await GetGuestBookingAsync(input.BookingId);
        if (booking is null || booking.Status != BookingStatus.Approved) return NotFound();
        if (!ModelState.IsValid) return View(input);
        try
        {
            var url = await fileStorage.SaveAsync(input.Document!, "documents", ".jpg", ".jpeg", ".png", ".pdf");
            context.GuestDocuments.Add(new GuestDocument { BookingId = booking.Id, DocumentType = input.DocumentType, DocumentUrl = url });
            await context.SaveChangesAsync();
            await notifications.CreateAsync(booking.Property.HostId, "Guest document submitted", $"A guest uploaded a document for booking #{booking.Id}.", "/Host/Dashboard", NotificationType.CheckIn);
            await logger.LogAsync("Guest document uploaded", booking.GuestId, null, $"Booking #{booking.Id}", HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["Success"] = "Document uploaded. Your host must verify it before check-in is completed.";
            return RedirectToAction(nameof(Dashboard));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(input);
        }
    }

    private async Task<Booking?> GetGuestBookingAsync(int id) => await context.Bookings
        .Include(b => b.Property).ThenInclude(p => p.CheckInProcess)
        .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == userManager.GetUserId(User));
}
