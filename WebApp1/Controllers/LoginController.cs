using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;
using WebApp1.Models;
using Microsoft.Data.SqlClient;
using System.Data;


namespace WebApp1.Controllers
{
    public class LoginController : Controller
    {
        // GET: LoginController
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Home"); ;
        }

        // GET: LoginController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: LoginController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: LoginController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: LoginController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: LoginController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: LoginController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: LoginController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        public ActionResult Login()
        {
            return RedirectToAction("Login", "Home");
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                string connectionString = "Data Source=weatherseeker2.database.windows.net;Initial Catalog=WeatherSeeker2;User ID=WeatherSeeker2;Password=WeatherMan2!;Trust Server Certificate=True";

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
                    return Index();
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
