using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        [HttpGet]
        [Authorize]
        public IActionResult Index() => View();

        [HttpGet]
        [AllowAnonymous]
        public IActionResult About() => View();

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Privacy() => View();

        [HttpGet]
        [Authorize]
        public IActionResult UserPage() => View();

        [HttpGet]
        [AllowAnonymous]
        public IActionResult RegisterClient() => View();

        /// <summary>
        /// Admin creation is restricted to existing admins. New deployments seed
        /// their first admin from BOOTSTRAP_ADMIN__USERNAME / __PASSWORD.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult RegisterAdmin() => View();

        [HttpGet]
        [AllowAnonymous]
        public IActionResult SuccessPage() => View();

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
