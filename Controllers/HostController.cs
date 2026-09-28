using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.ViewModels;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.Host)]
public class HostController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    INotificationService notifications,
    IActivityLogger logger) : Controller
{
    public async Task<IActionResult> Dashboard()
    {
        var hostId = userManager.GetUserId(User)!;
        var properties = await context.HostProperties.Include(p => p.PropertyImages).Where(p => p.HostId == hostId).ToListAsync();
        var propertyIds = properties.Select(p => p.Id).ToList();
        var pendingBookings = await context.Bookings.Include(b => b.Property).Include(b => b.Guest)
            .Where(b => propertyIds.Contains(b.PropertyId) && b.Status == BookingStatus.Pending).OrderBy(b => b.CheckInDate).ToListAsync();
        var pendingDocuments = await context.GuestDocuments.Include(d => d.Booking).ThenInclude(b => b.Property)
            .Include(d => d.Booking.Guest).Where(d => propertyIds.Contains(d.Booking.PropertyId) && d.Status == GuestDocumentStatus.Pending).ToListAsync();
        return View(new HostDashboardViewModel { Properties = properties, PendingBookings = pendingBookings, PendingDocuments = pendingDocuments });
    }

    public async Task<IActionResult> Bookings()
    {
        var hostId = userManager.GetUserId(User)!;
        var bookings = await context.Bookings.Include(b => b.Property).Include(b => b.Guest).Include(b => b.GuestDocuments)
            .Where(b => b.Property.HostId == hostId).OrderByDescending(b => b.CreatedAt).ToListAsync();
        return View(bookings);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBookingStatus(int id, bool approve)
    {
        var booking = await context.Bookings.Include(b => b.Property).Include(b => b.Guest)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property.HostId == userManager.GetUserId(User));
        if (booking is null) return NotFound();
        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "Only pending booking requests can be changed.";
            return RedirectToAction(nameof(Bookings));
        }
        booking.Status = approve ? BookingStatus.Approved : BookingStatus.Rejected;
        booking.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await notifications.CreateAsync(booking.GuestId, approve ? "Booking approved" : "Booking rejected",
            approve ? $"Your booking for {booking.Property.Title} is approved. Complete the check-in document step." : $"Your booking for {booking.Property.Title} was not approved.",
            "/Guest/Dashboard", NotificationType.Booking);
        await logger.LogAsync("Booking status updated", userManager.GetUserId(User), null, $"Booking #{booking.Id} set to {booking.Status}", HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["Success"] = $"Booking #{booking.Id} was {booking.Status.ToString().ToLowerInvariant()}.";
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var property = await context.HostProperties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == userManager.GetUserId(User));
        if (property is null) return NotFound();
        property.IsActive = !property.IsActive;
        await context.SaveChangesAsync();
        await logger.LogAsync("Property active status changed", property.HostId, null, $"Property #{property.Id}: {property.IsActive}", HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["Success"] = $"{property.Title} is now {(property.IsActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> CheckInProcess(int propertyId)
    {
        var property = await context.HostProperties.Include(p => p.CheckInProcess)
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.HostId == userManager.GetUserId(User));
        if (property is null) return NotFound();
        return View(new CheckInProcessInputModel
        {
            PropertyId = property.Id,
            Instructions = property.CheckInProcess?.Instructions ?? string.Empty,
            RequiredDocuments = property.CheckInProcess?.RequiredDocuments ?? "ID or Passport",
            IsEnabled = property.CheckInProcess?.IsEnabled ?? true
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckInProcess(CheckInProcessInputModel input)
    {
        var property = await context.HostProperties.Include(p => p.CheckInProcess)
            .FirstOrDefaultAsync(p => p.Id == input.PropertyId && p.HostId == userManager.GetUserId(User));
        if (property is null) return NotFound();
        if (!ModelState.IsValid) return View(input);
        if (property.CheckInProcess is null)
            property.CheckInProcess = new CheckInProcess { Instructions = input.Instructions, RequiredDocuments = input.RequiredDocuments, IsEnabled = input.IsEnabled };
        else
        {
            property.CheckInProcess.Instructions = input.Instructions;
            property.CheckInProcess.RequiredDocuments = input.RequiredDocuments;
            property.CheckInProcess.IsEnabled = input.IsEnabled;
            property.CheckInProcess.UpdatedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();
        await logger.LogAsync("Check-in process configured", property.HostId, null, $"Property #{property.Id}", HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["Success"] = "Check-in process saved.";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyDocument(int id, bool approve, string? comment)
    {
        var document = await context.GuestDocuments.Include(d => d.Booking).ThenInclude(b => b.Property)
            .FirstOrDefaultAsync(d => d.Id == id && d.Booking.Property.HostId == userManager.GetUserId(User));
        if (document is null) return NotFound();
        if (document.Status != GuestDocumentStatus.Pending) return RedirectToAction(nameof(Dashboard));
        document.Status = approve ? GuestDocumentStatus.Verified : GuestDocumentStatus.Rejected;
        document.HostComment = comment;
        document.ReviewedAt = DateTime.UtcNow;
        if (approve)
        {
            document.Booking.Status = BookingStatus.CheckedIn;
            document.Booking.UpdatedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();
        await notifications.CreateAsync(document.Booking.GuestId, approve ? "Check-in complete" : "Check-in document rejected",
            approve ? $"Your document for {document.Booking.Property.Title} was verified. You are checked in." : "Please upload a replacement document and contact your host if needed.",
            "/Guest/Dashboard", NotificationType.CheckIn);
        await logger.LogAsync("Guest document verified", userManager.GetUserId(User), null, $"Document #{document.Id}: {document.Status}", HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["Success"] = "Document review saved.";
        return RedirectToAction(nameof(Dashboard));
    }
}
