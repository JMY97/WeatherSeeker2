using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using WebApp1.Models;
using Microsoft.Data.SqlClient;
using System.Security.Claims;


namespace WebApp1.Controllers
{
    public class LoginController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<string> _passwordHasher = new();

        public LoginController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public ActionResult Login()
        {
            return RedirectToAction("Login", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");

                string selectQuery = @"
SELECT username, password, 'Admin' AS UserType FROM Admins WHERE username = @username
UNION ALL
SELECT username, password, 'Client' AS UserType FROM Clients WHERE username = @username";

                string? username = null;
                string? userType = null;
                var hasValidPassword = false;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(selectQuery, connection);
                    command.Parameters.AddWithValue("@username", model.Username);

                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var candidateUsername = reader.GetString(reader.GetOrdinal("username"));
                            var candidatePasswordHash = reader.GetString(reader.GetOrdinal("password"));
                            var verificationResult = _passwordHasher.VerifyHashedPassword(candidateUsername, candidatePasswordHash, model.Password);
                            if (verificationResult == PasswordVerificationResult.Success || verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                            {
                                username = candidateUsername;
                                userType = reader.GetString(reader.GetOrdinal("UserType"));
                                hasValidPassword = true;
                                break;
                            }
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
