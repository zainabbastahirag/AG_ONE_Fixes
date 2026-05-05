using Microsoft.AspNetCore.Mvc;

namespace AI.Baba.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
