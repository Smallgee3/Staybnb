using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public interface IActivityLogger
{
    Task LogAsync(string action, string? userId = null, string? userEmail = null, string? details = null, string? ipAddress = null);
}

public class ActivityLogger(ApplicationDbContext context) : IActivityLogger
{
    public async Task LogAsync(string action, string? userId = null, string? userEmail = null, string? details = null, string? ipAddress = null)
    {
        context.ActivityLogs.Add(new ActivityLog
        {
            Action = action,
            UserId = userId,
            UserEmail = userEmail,
            Details = details,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }
}

public interface INotificationService
{
    Task CreateAsync(string userId, string title, string message, string? link, NotificationType type);
}

public class NotificationService(ApplicationDbContext context) : INotificationService
{
    public async Task CreateAsync(string userId, string title, string message, string? link, NotificationType type)
    {
        context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Link = link,
            Type = type,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }
}

public interface IFileStorageService
{
    Task<string> SaveAsync(IFormFile file, string subFolder, params string[] allowedExtensions);
}

public class LocalFileStorageService(IWebHostEnvironment environment) : IFileStorageService
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    public async Task<string> SaveAsync(IFormFile file, string subFolder, params string[] allowedExtensions)
    {
        if (file.Length == 0 || file.Length > MaxFileSize)
            throw new InvalidOperationException("The uploaded file must be between 1 byte and 5 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            throw new InvalidOperationException("Unsupported file type.");

        var folder = Path.Combine(environment.WebRootPath, "uploads", subFolder);
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var destination = Path.Combine(folder, fileName);
        await using var stream = new FileStream(destination, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/{subFolder}/{fileName}";
    }
}

public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        foreach (var role in new[] { Roles.Guest, Roles.Host, Roles.Admin, Roles.SuperAdmin })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var email = configuration["SeedAdmin:Email"] ?? "superadmin@staybnb.local";
        var password = configuration["SeedAdmin:Password"] ?? "ChangeThis!123";
        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "System",
                LastName = "Administrator",
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Could not seed SuperAdmin: {string.Join(';', result.Errors.Select(x => x.Description))}");
        }
        if (!await userManager.IsInRoleAsync(admin, Roles.SuperAdmin))
            await userManager.AddToRoleAsync(admin, Roles.SuperAdmin);
    }
}
