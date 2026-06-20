using Microsoft.AspNetCore.Mvc;

namespace AI.Baba.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Auth() => View();
    public IActionResult Memory() => View();
    public IActionResult Personalities() => View();
    public IActionResult Avatars() => View();
    public IActionResult About() => View();
    public IActionResult Terms() => View();
}
