using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WebApp1.Data;
using WebApp1.Models;

namespace WebApp1.Controllers
{
    public class LoginController : Controller
    {
        private readonly DBContext _dbContext;
        private readonly ILogger<LoginController> _logger;
        private readonly PasswordHasher<string> _passwordHasher = new();

        public LoginController(DBContext dbContext, ILogger<LoginController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login() => RedirectToAction("Login", "Home");

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Message = "Enter both a username and a password.";
                return View("~/Views/Shared/Login.cshtml", model);
            }

            var (principal, matched) = await TryAuthenticateAsync(model.Username, model.Password);

            if (principal is null)
            {
                // One message for every failure mode: an unknown username and a
                // wrong password must be indistinguishable to the caller.
                _logger.LogWarning("Failed sign-in attempt for {Username}.", model.Username);
                ViewBag.Message = "Those credentials were not recognised.";
                return View("~/Views/Shared/Login.cshtml", model);
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                });

            _logger.LogInformation("{Username} signed in as {Role}.", matched.Name, matched.Role);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Home");
        }

        private async Task<(ClaimsPrincipal? Principal, (string Name, string Role) Matched)> TryAuthenticateAsync(
            string username,
            string password)
        {
            var admin = await _dbContext.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.username == username);
            if (admin is not null)
            {
                var result = _passwordHasher.VerifyHashedPassword(admin.username, admin.password, password);
                if (IsAccepted(result))
                {
                    return (BuildPrincipal(admin.username, "Admin"), (admin.username, "Admin"));
                }
            }

            var client = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.username == username);
            if (client is not null)
            {
                var result = _passwordHasher.VerifyHashedPassword(client.username, client.password, password);
                if (IsAccepted(result))
                {
                    return (BuildPrincipal(client.username, "Client"), (client.username, "Client"));
                }
            }

            return (null, default);
        }

        private static bool IsAccepted(PasswordVerificationResult result) =>
            result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;

        private static ClaimsPrincipal BuildPrincipal(string username, string role)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, username),
                new(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return new ClaimsPrincipal(identity);
        }
    }
}
