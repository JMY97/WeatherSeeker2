using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using WebApp1.Models;

namespace WebApp1.Controllers
{
    public class RegisterController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<string> _passwordHasher = new();

        public RegisterController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ActionResult Success()
        {
            //ViewBag.Message = "Data inserted successfully";
            return RedirectToAction("SuccessPage", "Home");
        }

        //Write to Database
        [HttpGet]
        public IActionResult RegisterClient()
        {

            return RedirectToAction("RegisterClient", "Home");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegisterClient(Clients user)
        {
            if (ModelState.IsValid)
            {
                // Insert data into database
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");
                string insertQuery = "INSERT INTO Clients(Id, username, password) VALUES(@Id, @username, @password)";
                string SelectQuery = "SELECT ClientId FROM Clients Where username = @username AND Id = @Id Order By ClientId Desc";
                string hashedPassword = _passwordHasher.HashPassword(user.username, user.password);

                int Id = 0;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(insertQuery, connection);
                    SqlCommand command2 = new SqlCommand(SelectQuery, connection);
                    //command.Parameters.AddWithValue("@ClientId", user.ClientId);
                    command.Parameters.AddWithValue("@Id", user.Id);
                    command.Parameters.AddWithValue("@username", user.username);
                    command.Parameters.AddWithValue("@password", hashedPassword);

                    connection.Open();
                    if (Id != user.Id)
                    {
                        command.ExecuteNonQuery();
                        command2.Parameters.AddWithValue("@Id", user.Id);
                        command2.Parameters.AddWithValue("@username", user.username);
                        command2.ExecuteNonQuery();
                        using (var reader = command2.ExecuteReader())
                        {
                            //Check the reader has data:
                            if (reader.Read())
                            {
                                user.ClientId = reader.GetInt32(reader.GetOrdinal("ClientId"));
                                //name = reader.GetString(reader.GetOrdinal("name"));

                            }

                        }
                        ViewBag.Message = "Client data inserted successfully. Your ClientID: " + user.ClientId;
                        return View();
                    }
                    
                }

                //ViewBag.Message = "Client data inserted successfully.";

                //return Success();
            }
            else
            {
                ViewBag.Message = "There are some errors on the page";
                return View(user);
            }
            return RegisterClient();
        }

        [HttpGet]
        public IActionResult RegisterAdmin()
        {
            return RedirectToAction("RegisterAdmin", "Home");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegisterAdmin(Admins user)
        {
            if (ModelState.IsValid)
            {
                // Insert data into database
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");
                string insertQuery = "INSERT INTO Admins(Id, username, password) VALUES(@Id, @username, @password)";
                string SelectQuery = "SELECT AdminId FROM Admins Where username = @username AND Id = @Id Order By AdminId Desc";
                string hashedPassword = _passwordHasher.HashPassword(user.username, user.password);
                int Id = 0;
                
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(insertQuery, connection);
                    SqlCommand command2 = new SqlCommand(SelectQuery, connection);
                    //command.Parameters.AddWithValue("@AdminId", user.AdminId);
                    command.Parameters.AddWithValue("@Id", user.Id);
                    command.Parameters.AddWithValue("@username", user.username);
                    command.Parameters.AddWithValue("@password", hashedPassword);

                    connection.Open();
                    if(@Id != user.Id)
                    {
                        command.ExecuteNonQuery();
                        command2.Parameters.AddWithValue("@Id", user.Id);
                        command2.Parameters.AddWithValue("@username", user.username);
                        command2.ExecuteNonQuery();
                        using (var reader = command2.ExecuteReader())
                        {
                            //Check the reader has data:
                            if (reader.Read())
                            {
                                user.AdminId = reader.GetInt32(reader.GetOrdinal("AdminId"));
                                //name = reader.GetString(reader.GetOrdinal("name"));

                            }

                        }
                        ViewBag.Message = "Admin data inserted successfully. Your AdminID: " + user.AdminId;
                        return View(user);
                    }
                    
                    
                }

                //ViewBag.Message = "Admin data inserted successfully.";

                //return Success();
            }
            else
            {
                ViewBag.Message = "There are some errors on the page";
                return View(user);
            }
            return RegisterAdmin();
        }

        [HttpGet]
        public IActionResult RegisterUser()
        {
            return RedirectToAction("UserPage", "Home");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegisterUser(Users user)
        {
            if (ModelState.IsValid)
            {
                // Insert data into database using ADO.NET
                string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");
                string insertQuery = "INSERT INTO Users(name) VALUES(@name)";
                string SelectQuery = "SELECT Id FROM Users Where name = @name Order By Id Desc";

                int Id = 0;
              
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(insertQuery, connection);
                    SqlCommand command2 = new SqlCommand(SelectQuery, connection);
                    //command.Parameters.AddWithValue("@Id", user.Id);
                    command.Parameters.AddWithValue("@name", user.name);

                    connection.Open();
                    command.ExecuteNonQuery();
                    
                    //command2.Parameters.AddWithValue("@Id", user.Id);
                   
                    command2.Parameters.AddWithValue("@name", user.name);
                    command2.ExecuteNonQuery();

                    using (var reader = command2.ExecuteReader())
                    {
                        //Check the reader has data:
                        if (reader.Read())
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id"));
                            //name = reader.GetString(reader.GetOrdinal("name"));

                        }

                    }



                    //ViewBag.Message = "Your name is: " + name + "\n" + "Your Id is: " + Id;
                }
                if (Id != 0)
                {
                    ViewBag.Message = "Hello " + user.name + ",  " + "your Id is " + Id;
                }
            }
            else
            {
                ViewBag.Message = "There are some errors on the page";
                return View(user);
            }
            return View(user);
        }
    }
}
