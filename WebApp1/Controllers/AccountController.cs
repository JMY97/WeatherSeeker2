using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp1.Data;
using WebApp1.Models;

namespace WebApp1.Controllers
{
    [Authorize]
    [Route("account")]
    public class AccountController : Controller
    {
        private readonly DBContext _dbContext;
        private readonly ILogger<AccountController> _logger;
        private readonly PasswordHasher<Account> _passwordHasher = new();

        public AccountController(DBContext dbContext, ILogger<AccountController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("settings")]
        public async Task<IActionResult> Settings()
        {
            var account = await GetCurrentAccountAsync();
            if (account is null)
            {
                return await RequireReauthenticationAsync();
            }

            return View("~/Views/Home/AccountSettings.cshtml", ToViewModel(account));
        }

        /// <summary>
        /// Deletes the signed-in account and its data immediately, then drops the
        /// session. The password is re-checked so a borrowed session cannot wipe
        /// the account.
        /// </summary>
        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(AccountSettingsViewModel model)
        {
            var account = await GetCurrentAccountAsync();
            if (account is null)
            {
                return await RequireReauthenticationAsync();
            }

            if (!ModelState.IsValid)
            {
                return View("~/Views/Home/AccountSettings.cshtml", KeepDetails(account, model));
            }

            var verify = VerifyPassword(account, model.Password);

            if (verify is PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(nameof(model.Password), "That password is not correct.");
                _logger.LogWarning("Account {AccountId} failed a delete confirmation.", account.Id);

                return View("~/Views/Home/AccountSettings.cshtml", KeepDetails(account, model));
            }

            // Everything we hold about this account is one row, so the delete is
            // atomic and immediate. It is keyed by id inside the query so a
            // concurrent removal cannot delete a different row.
            var removed = await _dbContext.Accounts
                .Where(a => a.Id == account.Id)
                .ExecuteDeleteAsync();

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (removed == 0)
            {
                // Already gone: the session ends either way, so a stale cookie can
                // never describe a row that no longer exists.
                return RedirectToAction("Login", "Home");
            }

            _logger.LogInformation("Account {AccountId} deleted itself.", account.Id);

            return RedirectToAction("Deleted", "Home");
        }

        private Task<Account?> GetCurrentAccountAsync()
        {
            var id = AccountSession.GetAccountId(User);

            return id is null
                ? Task.FromResult<Account?>(null)
                : _dbContext.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id.Value);
        }

        /// <summary>
        /// Verifies without letting a malformed stored hash throw. A row carried
        /// over by the Accounts migration can hold a pre-hashing plaintext
        /// password, which <see cref="PasswordHasher{TUser}"/> cannot parse. Such a
        /// row cannot confirm a delete, but it must report a wrong password rather
        /// than a server error, so the account stays deletable rather than stuck.
        /// </summary>
        private PasswordVerificationResult VerifyPassword(Account account, string password)
        {
            try
            {
                return _passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password);
            }
            catch (FormatException)
            {
                return PasswordVerificationResult.Failed;
            }
        }

        private static AccountSettingsViewModel ToViewModel(Account account) => new()
        {
            Username = account.Username,
            CreatedAt = account.CreatedAt
        };

        /// <summary>
        /// Re-renders the page after a rejected attempt. The details are read from
        /// the database row, never from the posted body, and the password is
        /// dropped so it is not echoed back into the form.
        /// </summary>
        private static AccountSettingsViewModel KeepDetails(Account account, AccountSettingsViewModel posted) => new()
        {
            Username = account.Username,
            CreatedAt = account.CreatedAt,
            Password = string.Empty
        };

        /// <summary>
        /// The cookie outlived its row (deleted from another session, or a reset
        /// database), so clear it before sending the caller to sign in again.
        /// </summary>
        private async Task<IActionResult> RequireReauthenticationAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Login", "Home");
        }
    }
}
