using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using WebApp1.Models;

namespace WebApp1.Controllers
{
    /// <summary>
    /// Creates and issues the authentication cookie. Centralised so registration
    /// and login cannot drift apart in the claims they mint.
    /// </summary>
    internal static class AccountSession
    {
        /// <summary>Claim carrying the account's current session version.</summary>
        internal const string SessionVersionClaim = "ws:sv";

        public static async Task SignInAsync(HttpContext httpContext, Account account)
        {
            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.Name, account.Username),
                    // The id is the stable handle for "delete my own row"; the
                    // username is not used for lookups because it can be reused
                    // after an account is deleted.
                    new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
                    // Signing out rotates this, which invalidates the cookie that
                    // produced the request even though the ticket itself is
                    // self-contained.
                    new Claim(SessionVersionClaim, account.SessionVersion.ToString("N"))
                },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                });
        }

        /// <summary>Returns the signed-in account id, or null when absent/invalid.</summary>
        public static int? GetAccountId(ClaimsPrincipal user)
        {
            var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }

        /// <summary>
        /// True when the cookie's session version still matches the stored one.
        /// A mismatch means the account signed out (or was rotated) after this
        /// cookie was issued, so the cookie must no longer be accepted.
        /// </summary>
        public static bool MatchesSessionVersion(ClaimsPrincipal user, Account account) =>
            Guid.TryParse(user.FindFirstValue(SessionVersionClaim), out var presented)
            && presented == account.SessionVersion;
    }
}
