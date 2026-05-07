using Microsoft.AspNetCore.Mvc;
using WebApp1.Models;
using Microsoft.Data.SqlClient;


namespace WebApp1.Controllers
{
    public class LoginController : Controller
    {
        private readonly IConfiguration _configuration;

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
        public IActionResult Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");

                string SelectQuery = "SELECT username, password FROM Admins WHERE username = @username AND password = @password UNION ALL SELECT username, password FROM Clients WHERE username = @username AND password = @password";

                string username = "Username";
                string password = "Password";

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(SelectQuery, connection);
                    command.Parameters.AddWithValue("@username", model.Username);
                    command.Parameters.AddWithValue("@password", model.Password);

                    connection.Open();
                    command.ExecuteNonQuery();

                    using (var reader = command.ExecuteReader())
                    {
                        //Check the reader has data:
                        if (reader.Read())
                        {
                            username = reader.GetString(reader.GetOrdinal("username"));
                            password = reader.GetString(reader.GetOrdinal("password"));

                        }

                    }

                }
                if (username == model.Username && password == model.Password)
                {
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
    }
}
