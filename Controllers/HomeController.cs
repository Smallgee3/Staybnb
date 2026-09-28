using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.ViewModels;

namespace Staybnb.Controllers;

public class HomeController(ApplicationDbContext context) : Controller
{
    public async Task<IActionResult> Index(string? city, string? propertyType)
    {
        var query = context.HostProperties
            .Where(p => p.IsActive)
            .Include(p => p.PropertyImages)
            .Include(p => p.Reviews)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(p => p.City == city);
        if (!string.IsNullOrWhiteSpace(propertyType))
            query = query.Where(p => p.PropertyType == propertyType);

        return View(new PropertySearchViewModel
        {
            City = city,
            PropertyType = propertyType,
            Properties = await query.OrderByDescending(p => p.CreatedAt).ToListAsync()
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var property = await context.HostProperties
            .Include(p => p.Host)
            .Include(p => p.PropertyImages)
            .Include(p => p.PropertyAmenities).ThenInclude(pa => pa.Amenity)
            .Include(p => p.Reviews).ThenInclude(r => r.Reviewer)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        return property is null ? NotFound() : View(property);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
