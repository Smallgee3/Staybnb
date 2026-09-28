using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Controllers;

[Authorize]
public class NotificationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = userManager.GetUserId(User)!;
        return View(await context.Notifications.Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var notification = await context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userManager.GetUserId(User));
        if (notification is null) return NotFound();
        notification.IsRead = true;
        await context.SaveChangesAsync();
        return string.IsNullOrWhiteSpace(notification.Link) ? RedirectToAction(nameof(Index)) : LocalRedirect(notification.Link);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var notifications = await context.Notifications.Where(n => n.UserId == userManager.GetUserId(User) && !n.IsRead).ToListAsync();
        foreach (var notification in notifications) notification.IsRead = true;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
