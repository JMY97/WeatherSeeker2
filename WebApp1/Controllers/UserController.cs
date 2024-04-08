using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApp1.Models;

namespace WebApp1.Controllers
{
    public class UserController : Controller
    {
        
        [HttpGet]
        public IActionResult InsertUser()
        {
            return View();
        }
        [HttpPost]
        public IActionResult InsertUser(Users user) 
        {
            if (ModelState.IsValid)
            {
                // Insert data into database using ADO.NET
                string connectionString = "Data Source=DESKTOP-5BRRAB8;Initial Catalog=WeatherSeeker2;Integrated Security=True;Trust Server Certificate=True";
                string insertQuery = "INSERT INTO Users(Id, name) VALUES(@Id, @name)";

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(insertQuery, connection);
                    command.Parameters.AddWithValue("@Id", user.Id);
                    command.Parameters.AddWithValue("@name", user.name);

                    connection.Open();
                    command.ExecuteNonQuery();
                }

                ViewBag.Message = "User data inserted successfully.";
            }
            else
            {
                ViewBag.Message = "There are some errors on the page.";
                return View(user);
            }
            return View();
        }
    }
}
