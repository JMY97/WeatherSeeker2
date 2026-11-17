using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WebApp1.Data;
using WebApp1.Models;

namespace WebApp1.Controllers
{
    public class RegisterController : Controller
    {
        private readonly DBContext _dbContext;
        private readonly ILogger<RegisterController> _logger;
        private readonly PasswordHasher<string> _passwordHasher = new();

        public RegisterController(DBContext dbContext, ILogger<RegisterController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult RegisterClient() => RedirectToAction("RegisterClient", "Home");

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        [EnableRateLimiting("signup")]
        public async Task<IActionResult> RegisterClient(Clients user)
        {
            ModelState.Remove(nameof(Clients.Id));
            ModelState.Remove(nameof(Clients.ClientId));

            if (!ModelState.IsValid)
            {
                return View("~/Views/Shared/RegisterClient.cshtml", user);
            }

            var username = user.username.Trim();

            if (await _dbContext.Clients.AnyAsync(c => c.username == username) ||
                await _dbContext.Admins.AnyAsync(a => a.username == username))
            {
                ModelState.AddModelError(nameof(Clients.username), "Username is already taken.");
                ViewBag.Message = "Username is already taken.";
                return View("~/Views/Shared/RegisterClient.cshtml", user);
            }

            var newUser = new Users { name = username };
            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();

            user.Id = newUser.Id;
            user.username = username;
            user.password = _passwordHasher.HashPassword(username, user.password);

            _dbContext.Clients.Add(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Client account {Username} created.", username);

            ViewBag.Message = $"Account created. Your client ID is {user.ClientId}.";
            return View("~/Views/Shared/SuccessPage.cshtml");
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult RegisterAdmin() => RedirectToAction("RegisterAdmin", "Home");

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> RegisterAdmin(Admins user)
        {
            ModelState.Remove(nameof(Admins.Id));
            ModelState.Remove(nameof(Admins.AdminId));

            if (!ModelState.IsValid)
            {
                return View("~/Views/Shared/RegisterAdmin.cshtml", user);
            }

            var username = user.username.Trim();

            if (await _dbContext.Admins.AnyAsync(a => a.username == username) ||
                await _dbContext.Clients.AnyAsync(c => c.username == username))
            {
                ModelState.AddModelError(nameof(Admins.username), "Username is already taken.");
                ViewBag.Message = "Username is already taken.";
                return View("~/Views/Shared/RegisterAdmin.cshtml", user);
            }

            var newUser = new Users { name = username };
            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();

            user.Id = newUser.Id;
            user.username = username;
            user.password = _passwordHasher.HashPassword(username, user.password);

            _dbContext.Admins.Add(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Admin account {Username} created by {Actor}.",
                username,
                User.Identity?.Name);

            ViewBag.Message = $"Admin account created. Its admin ID is {user.AdminId}.";
            return View("~/Views/Shared/SuccessPage.cshtml");
        }

        [HttpGet]
        [Authorize]
        public IActionResult RegisterUser() => RedirectToAction("UserPage", "Home");

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RegisterUser(Users user)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Home/UserPage.cshtml", user);
            }

            var newUser = new Users { name = user.name.Trim() };
            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();

            ViewBag.Message = $"Added {newUser.name} to the directory as ID {newUser.Id}.";
            ModelState.Clear();
            return View("~/Views/Home/UserPage.cshtml", new Users { name = string.Empty });
        }
    }
}
