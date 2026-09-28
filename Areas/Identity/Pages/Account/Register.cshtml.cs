using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Staybnb.Models;
using Staybnb.Services;
using System.ComponentModel.DataAnnotations;

namespace Staybnb.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel(
    UserManager<ApplicationUser> userManager,
    IUserStore<ApplicationUser> userStore,
    ILogger<RegisterModel> logger,
    IActivityLogger activityLogger) : PageModel
{
    private readonly IUserEmailStore<ApplicationUser> _emailStore = GetEmailStore(userManager, userStore);

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required, Display(Name = "First name")] public string FirstName { get; set; } = string.Empty;
        [Required, Display(Name = "Last name")] public string LastName { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 8), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        [DataType(DataType.Password), Display(Name = "Confirm password"), Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")] public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        if (!ModelState.IsValid) return Page();

        var user = new ApplicationUser
        {
            FirstName = Input.FirstName.Trim(),
            LastName = Input.LastName.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        await userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
        await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
        var result = await userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            var roleResult = await userManager.AddToRoleAsync(user, Roles.Guest);
            if (!roleResult.Succeeded)
            {
                await userManager.DeleteAsync(user);
                foreach (var error in roleResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
            logger.LogInformation("A new Guest account was created.");
            await activityLogger.LogAsync("Guest account registered", user.Id, user.Email, "New registrant assigned Guest role", HttpContext.Connection.RemoteIpAddress?.ToString());
            return LocalRedirect(ReturnUrl);
        }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return Page();
    }

    private static IUserEmailStore<ApplicationUser> GetEmailStore(UserManager<ApplicationUser> userManager, IUserStore<ApplicationUser> userStore)
    {
        if (!userManager.SupportsUserEmail) throw new NotSupportedException("The default UI requires a user store with email support.");
        return (IUserEmailStore<ApplicationUser>)userStore;
    }
}
