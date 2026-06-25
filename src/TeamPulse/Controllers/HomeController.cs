using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TeamPulse.Models;

namespace TeamPulse.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    public IActionResult AccessDenied() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? id)
    {
        if (id.HasValue)
        {
            Response.StatusCode = id.Value;
        }

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = id
        });
    }
}
