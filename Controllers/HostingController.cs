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
public class HostingController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IFileStorageService fileStorage,
    IActivityLogger logger) : Controller
{
    public async Task<IActionResult> Apply()
    {
        var userId = userManager.GetUserId(User)!;
        var application = await context.HostApplications.OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.ApplicantId == userId);
        if (application is not null)
        {
            TempData["Info"] = "You already have an application. Its current status is " + application.Status + ".";
            return RedirectToAction(nameof(MyApplication));
        }
        return View(new HostApplicationInputModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(HostApplicationInputModel input)
    {
        if (input.Images is null || input.Images.Count == 0 || input.Images.Any(x => x.Length == 0))
            ModelState.AddModelError(nameof(input.Images), "At least one valid property image is required before submitting.");
        if (!ModelState.IsValid) return View(input);

        var userId = userManager.GetUserId(User)!;
        if (await context.HostApplications.AnyAsync(x => x.ApplicantId == userId && x.Status == HostApplicationStatus.Pending))
        {
            ModelState.AddModelError(string.Empty, "You already have a pending host application.");
            return View(input);
        }

        var application = new HostApplication
        {
            ApplicantId = userId,
            Title = input.Title,
            Description = input.Description,
            PricePerNight = input.PricePerNight,
            CleaningFee = input.CleaningFee,
            ServiceFee = input.ServiceFee,
            Address = input.Address,
            City = input.City,
            PropertyType = input.PropertyType
        };
        context.HostApplications.Add(application);
        await context.SaveChangesAsync();

        try
        {
            var order = 0;
            foreach (var image in input.Images!)
            {
                var url = await fileStorage.SaveAsync(image, "applications", ".jpg", ".jpeg", ".png", ".webp");
                context.PropertyImages.Add(new PropertyImage { HostApplicationId = application.Id, ImageUrl = url, SortOrder = order++ });
            }
            await context.SaveChangesAsync();
            var user = await userManager.GetUserAsync(User);
            await logger.LogAsync("Host application submitted", userId, user?.Email, $"Application #{application.Id}: {application.Title}", HttpContext.Connection.RemoteIpAddress?.ToString());
            TempData["Success"] = "Your host application was submitted for administrator review.";
            return RedirectToAction(nameof(MyApplication));
        }
        catch (InvalidOperationException ex)
        {
            context.HostApplications.Remove(application);
            await context.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(input);
        }
    }

    public async Task<IActionResult> MyApplication()
    {
        var userId = userManager.GetUserId(User)!;
        var application = await context.HostApplications.Include(x => x.PropertyImages)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(x => x.ApplicantId == userId);
        return View(application);
    }
}
