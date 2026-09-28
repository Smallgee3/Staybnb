using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.ViewModels;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize]
public class MessagesController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    INotificationService notifications,
    IActivityLogger logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = userManager.GetUserId(User)!;
        var messages = await context.UserMessages.Include(m => m.Sender).Include(m => m.Recipient)
            .Where(m => m.SenderId == userId || m.RecipientId == userId)
            .OrderByDescending(m => m.SentAt).ToListAsync();
        return View(messages);
    }

    public async Task<IActionResult> Compose(string? recipientId, string? subject)
    {
        ViewBag.Users = await context.Users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();
        return View(new ComposeMessageInputModel { RecipientId = recipientId ?? string.Empty, Subject = subject ?? string.Empty });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Compose(ComposeMessageInputModel input)
    {
        var senderId = userManager.GetUserId(User)!;
        if (input.RecipientId == senderId) ModelState.AddModelError(nameof(input.RecipientId), "You cannot send a message to yourself.");
        var recipient = await userManager.FindByIdAsync(input.RecipientId);
        if (recipient is null) ModelState.AddModelError(nameof(input.RecipientId), "Choose a valid recipient.");
        if (!ModelState.IsValid)
        {
            ViewBag.Users = await context.Users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();
            return View(input);
        }
        var message = new UserMessage { SenderId = senderId, RecipientId = input.RecipientId, Subject = input.Subject, Body = input.Body };
        context.UserMessages.Add(message);
        await context.SaveChangesAsync();
        await notifications.CreateAsync(input.RecipientId, "New message", $"You received a message: {input.Subject}", "/Messages", NotificationType.Message);
        await logger.LogAsync("Message sent", senderId, null, $"Message #{message.Id} to {recipient!.Email}", HttpContext.Connection.RemoteIpAddress?.ToString());
        TempData["Success"] = "Message sent.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Read(int id)
    {
        var userId = userManager.GetUserId(User)!;
        var message = await context.UserMessages.Include(m => m.Sender).Include(m => m.Recipient)
            .FirstOrDefaultAsync(m => m.Id == id && (m.SenderId == userId || m.RecipientId == userId));
        if (message is null) return NotFound();
        if (message.RecipientId == userId && !message.IsRead)
        {
            message.IsRead = true;
            await context.SaveChangesAsync();
        }
        return View(message);
    }
}
