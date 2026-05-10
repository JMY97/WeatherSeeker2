using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using WebApp1.Models;
using WebApp1.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


namespace WebApp1.Controllers
{
    public class LoginController : Controller
    {
        private readonly DBContext _dbContext;
        private readonly PasswordHasher<string> _passwordHasher = new();

        public LoginController(DBContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public ActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return RedirectToAction("Login", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                string? username = null;
                string? userType = null;
                var hasValidPassword = false;

                var admin = await _dbContext.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.username == model.Username);
                if (admin != null)
                {
                    var adminVerify = _passwordHasher.VerifyHashedPassword(admin.username, admin.password, model.Password);
                    if (adminVerify == PasswordVerificationResult.Success || adminVerify == PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        username = admin.username;
                        userType = "Admin";
                        hasValidPassword = true;
                    }
                }

                if (!hasValidPassword)
                {
                    var client = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.username == model.Username);
                    if (client != null)
                    {
                        var clientVerify = _passwordHasher.VerifyHashedPassword(client.username, client.password, model.Password);
                        if (clientVerify == PasswordVerificationResult.Success || clientVerify == PasswordVerificationResult.SuccessRehashNeeded)
                        {
                            username = client.username;
                            userType = "Client";
                            hasValidPassword = true;
                        }
                    }
                }

                if (hasValidPassword && username != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, username),
                        new Claim(ClaimTypes.Role, userType ?? "User")
                    };
                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    ViewBag.Message = "Welcome to Weather Seeker";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ViewBag.Message = "Unsuccessful Login Attempt";
                    return View(model);
                }

            }

            ViewBag.Message = "There are some errors on the page";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Home");
        }
    }
}
