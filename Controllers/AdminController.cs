using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.ViewModels;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.SuperAdmin)]
public class AdminController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    INotificationService notifications,
    IActivityLogger logger) : Controller
{
    public async Task<IActionResult> Dashboard()
    {
        return View(new AdminDashboardViewModel
        {
            PendingApplications = await context.HostApplications.CountAsync(a => a.Status == HostApplicationStatus.Pending),
            PendingBookings = await context.Bookings.CountAsync(b => b.Status == BookingStatus.Pending),
            RegisteredUsers = await context.Users.CountAsync(),
            RecentActivities = await context.ActivityLogs.OrderByDescending(a => a.CreatedAt).Take(10).ToListAsync()
        });
    }

    public async Task<IActionResult> Applications()
    {
        var applications = await context.HostApplications.Include(a => a.Applicant).Include(a => a.PropertyImages)
            .OrderBy(a => a.Status).ThenByDescending(a => a.CreatedAt).ToListAsync();
        return View(applications);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewApplication(ReviewApplicationInputModel input)
    {
        var application = await context.HostApplications.Include(a => a.Applicant).Include(a => a.PropertyImages)
            .FirstOrDefaultAsync(a => a.Id == input.Id && a.Status == HostApplicationStatus.Pending);
        if (application is null) return NotFound();
        if (input.Approve && application.PropertyImages.Count == 0)
        {
            TempData["Error"] = "This application cannot be approved because it has no property image.";
            return RedirectToAction(nameof(Applications));
        }

        application.Status = input.Approve ? HostApplicationStatus.Approved : HostApplicationStatus.Rejected;
        application.AdminComment = input.AdminComment;
        application.ReviewedAt = DateTime.UtcNow;

        if (input.Approve)
        {
            var addHostResult = await userManager.AddToRoleAsync(application.Applicant, Roles.Host);
            if (!addHostResult.Succeeded)
            {
                TempData["Error"] = string.Join(" ", addHostResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Applications));
            }
            if (await userManager.IsInRoleAsync(application.Applicant, Roles.Guest))
            {
                var removeGuestResult = await userManager.RemoveFromRoleAsync(application.Applicant, Roles.Guest);
                if (!removeGuestResult.Succeeded)
                {
                    await userManager.RemoveFromRoleAsync(application.Applicant, Roles.Host);
                    TempData["Error"] = "Could not complete the Guest-to-Host role transition.";
                    return RedirectToAction(nameof(Applications));
                }
            }

            var property = new HostProperty
            {
                Title = application.Title,
                Description = application.Description,
                PricePerNight = application.PricePerNight,
                CleaningFee = application.CleaningFee,
                ServiceFee = application.ServiceFee,
                Address = application.Address,
                City = application.City,
                PropertyType = application.PropertyType,
                HostId = application.ApplicantId,
                IsActive = true
            };
            context.HostProperties.Add(property);
            foreach (var image in application.PropertyImages)
            {
                image.HostApplicationId = null;
                image.HostProperty = property;
            }
            await context.SaveChangesAsync();
            await notifications.CreateAsync(application.ApplicantId, "Host application approved", "You are now a Host. Your first listing has been published.", "/Host/Dashboard", NotificationType.Application);
            await logger.LogAsync("Host application approved and roles swapped", userManager.GetUserId(User), null, $"Applicant {application.Applicant.Email}; application #{application.Id}", HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        else
        {
            await context.SaveChangesAsync();
            await notifications.CreateAsync(application.ApplicantId, "Host application rejected", input.AdminComment ?? "Your application was not approved. Please contact support for details.", "/Hosting/MyApplication", NotificationType.Application);
            await logger.LogAsync("Host application rejected", userManager.GetUserId(User), null, $"Application #{application.Id}", HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        TempData["Success"] = input.Approve ? "Application approved; the account is now a Host." : "Application rejected.";
        return RedirectToAction(nameof(Applications));
    }

    public async Task<IActionResult> Logs()
    {
        return View(await context.ActivityLogs.OrderByDescending(a => a.CreatedAt).Take(250).ToListAsync());
    }

    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> UserManagement(string? query)
    {
        var users = context.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
            users = users.Where(u => u.Email!.Contains(query) || u.FirstName.Contains(query) || u.LastName.Contains(query));
        var result = await users.OrderBy(u => u.Email).Take(100).ToListAsync();
        ViewBag.UserRoles = result.ToDictionary(u => u.Id, u => userManager.GetRolesAsync(u).Result);
        ViewBag.Query = query;
        return View(result);
    }

    [Authorize(Roles = Roles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteToAdmin(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (await userManager.IsInRoleAsync(user, Roles.SuperAdmin))
        {
            TempData["Error"] = "SuperAdmin accounts cannot be altered here.";
            return RedirectToAction(nameof(UserManagement));
        }
        if (!await userManager.IsInRoleAsync(user, Roles.Guest))
        {
            TempData["Error"] = "Only existing Guests can be promoted to Admin.";
            return RedirectToAction(nameof(UserManagement));
        }
        var result = await userManager.AddToRoleAsync(user, Roles.Admin);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(UserManagement));
        }
        await userManager.RemoveFromRoleAsync(user, Roles.Guest);
        await logger.LogAsync("Guest promoted to Admin", userManager.GetUserId(User), null, $"Promoted {user.Email}", HttpContext.Connection.RemoteIpAddress?.ToString());
        await notifications.CreateAsync(user.Id, "Administrator access granted", "A SuperAdmin promoted your account to Administrator.", "/Admin/Dashboard", NotificationType.General);
        TempData["Success"] = $"{user.Email} is now an Administrator.";
        return RedirectToAction(nameof(UserManagement));
    }
}
