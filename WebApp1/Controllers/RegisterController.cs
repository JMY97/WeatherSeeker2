using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WebApp1.Data;
using WebApp1.Models;

namespace WebApp1.Controllers
{
    /// <summary>
    /// Account creation. Anonymous by design: this is the only way in, so it must
    /// never sit behind the sign-in it exists to provide.
    /// </summary>
    public class RegisterController : Controller
    {
        private readonly DBContext _dbContext;
        private readonly ILogger<RegisterController> _logger;
        private readonly PasswordHasher<Account> _passwordHasher = new();

        public RegisterController(DBContext dbContext, ILogger<RegisterController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Shared/Register.cshtml", new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        [EnableRateLimiting("signup")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Shared/Register.cshtml", model);
            }

            var username = Account.Normalize(model.Username);

            if (await _dbContext.Accounts.AnyAsync(a => a.Username == username))
            {
                ModelState.AddModelError(nameof(RegisterViewModel.Username), "That username is already taken.");
                return View("~/Views/Shared/Register.cshtml", model);
            }

            var account = new Account
            {
                Username = username,
                CreatedAt = DateTimeOffset.UtcNow
            };
            account.PasswordHash = _passwordHasher.HashPassword(account, model.Password);

            _dbContext.Accounts.Add(account);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The unique index caught a sign-up that raced this one; report it
                // as a taken username rather than a server error.
                _dbContext.Entry(account).State = EntityState.Detached;
                ModelState.AddModelError(nameof(RegisterViewModel.Username), "That username is already taken.");
                return View("~/Views/Shared/Register.cshtml", model);
            }

            _logger.LogInformation("Account {AccountId} created.", account.Id);

            // Sign-up and sign-in are the same door, and there is nothing further
            // to wait for, so the new account starts signed in.
            await AccountSession.SignInAsync(HttpContext, account);

            return RedirectToAction("Index", "Home");
        }
    }
}
