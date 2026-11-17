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
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ReturnUrl = SafeLocalUrl(returnUrl);

            return View("~/Views/Shared/Login.cshtml", new LoginViewModel());
        }

        /// <summary>
        /// Returns the path only when it is a site-relative URL, so a crafted
        /// returnUrl cannot send a freshly signed-in user to another origin.
        /// </summary>
        internal static string? SafeLocalUrl(string? url) =>
            !string.IsNullOrWhiteSpace(url)
            && url.Length > 1
            && url[0] == '/'
            && url[1] != '/'
            && url[1] != '\\'
            && !url.Contains("://", StringComparison.Ordinal)
                ? url
                : null;

        /// <summary>
        /// Shown after an account deletes itself. Anonymous on purpose: the session
        /// is already gone, so this page confirms rather than protects.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Deleted() => View();

        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
