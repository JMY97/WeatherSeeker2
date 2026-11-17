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
        /// <summary>
        /// A real hash of a value nobody knows, used only to spend the same
        /// key-derivation time as a genuine verification when there is nothing valid
        /// to compare against. Random per process, so no submitted password can
        /// ever match it.
        /// </summary>
        private static readonly string DummyPasswordHash =
            new PasswordHasher<Account>().HashPassword(
                new Account(),
                Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));

        private readonly DBContext _dbContext;
        private readonly ILogger<LoginController> _logger;
        private readonly PasswordHasher<Account> _passwordHasher = new();

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
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl)
        {
            ViewBag.ReturnUrl = HomeController.SafeLocalUrl(returnUrl);

            if (!ModelState.IsValid)
            {
                ViewBag.Message = "Enter both a username and a password.";
                return View("~/Views/Shared/Login.cshtml", model);
            }

            var account = await _dbContext.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Username == Account.Normalize(model.Username));

            // Evaluated before any null check: passing null runs the same
            // key-derivation work as a real verification, so an unknown username
            // and a wrong password cannot be told apart by response time.
            var accepted = VerifyPassword(account, model.Password)
                is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;

            if (!accepted || account is null)
            {
                // One message for every failure mode: the caller must not learn
                // which half of the pair was wrong.
                _logger.LogWarning("Failed sign-in attempt for {Username}.", model.Username);
                ViewBag.Message = "Those credentials were not recognised.";
                return View("~/Views/Shared/Login.cshtml", model);
            }

            await AccountSession.SignInAsync(HttpContext, account);

            _logger.LogInformation("Account {AccountId} signed in.", account.Id);

            // returnUrl was filtered to a site-relative path on the way in, so this
            // cannot leave the site.
            return Redirect(ViewBag.ReturnUrl is string target ? target : Url.Action("Index", "Home")!);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            // Rotate the stored session version so the cookie that just signed out
            // cannot be replayed: the ticket is self-contained, so clearing it in
            // the browser alone would leave a captured copy valid for hours.
            var id = AccountSession.GetAccountId(User);

            if (id is not null)
            {
                await _dbContext.Accounts
                    .Where(a => a.Id == id.Value)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(a => a.SessionVersion, Guid.NewGuid()));
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Home");
        }

        /// <summary>
        /// Verifies a password without letting a malformed stored hash throw, and
        /// spends the same key-derivation time whether or not the account exists.
        /// </summary>
        /// <remarks>
        /// Both cases matter here. A row carried over by the Accounts migration can
        /// still hold a pre-hashing plaintext password, which makes
        /// <see cref="PasswordHasher{TUser}"/> throw. And an unknown username must
        /// not answer faster than a wrong password, or the difference reveals which
        /// accounts exist, so every failure path runs a real verification against
        /// <see cref="DummyPasswordHash"/>.
        /// </remarks>
        private PasswordVerificationResult VerifyPassword(Account? account, string password)
        {
            if (account is null || string.IsNullOrEmpty(account.PasswordHash))
            {
                return VerifyAgainstDummy(password);
            }

            try
            {
                return _passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password);
            }
            catch (FormatException)
            {
                return VerifyAgainstDummy(password);
            }
        }

        private PasswordVerificationResult VerifyAgainstDummy(string password)
        {
            _passwordHasher.VerifyHashedPassword(new Account(), DummyPasswordHash, password);

            return PasswordVerificationResult.Failed;
        }
    }
}
