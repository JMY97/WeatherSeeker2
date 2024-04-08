using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WebApp1.Models;

namespace WebApp1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            ViewBag.message = "Welcome to Weather Seeker";
            return View();
            
        }
        public IActionResult About()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult RegisterClient()
        {
            return View();
        }

        public IActionResult RegisterAdmin()
        {
            return View();
        }

        public IActionResult UserPage()
        {
            return View();
        }

        public IActionResult SuccessPage()
        {
            return View();
        }
        public IActionResult Login()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
