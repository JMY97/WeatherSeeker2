using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using WebApp1.Models;
using WebApp1.Data;
using Microsoft.EntityFrameworkCore;

namespace WebApp1.Controllers
{
    public class RegisterController : Controller
    {
        private readonly DBContext _dbContext;
        private readonly PasswordHasher<string> _passwordHasher = new();

        public RegisterController(DBContext dbContext)
        {
            _dbContext = dbContext;
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
        public async Task<IActionResult> RegisterClient(Clients user)
        {
            ModelState.Remove("Id");
            if (ModelState.IsValid)
            {
                var existingClient = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.username == user.username);
                if (existingClient != null)
                {
                    ModelState.AddModelError("username", "Username is already taken.");
                    ViewBag.Message = "Username is already taken.";
                    return View(user);
                }

                var newUser = new Users { name = user.username };
                _dbContext.Users.Add(newUser);
                await _dbContext.SaveChangesAsync();
                user.Id = newUser.Id;

                user.password = _passwordHasher.HashPassword(user.username, user.password);
                _dbContext.Clients.Add(user);
                await _dbContext.SaveChangesAsync();
                ViewBag.Message = "Client data inserted successfully. Your ClientID: " + user.ClientId;
                return View(user);
            }
            else
            {
                ViewBag.Message = "There are some errors on the page";
                return View(user);
            }
        }

        [HttpGet]
        public IActionResult RegisterAdmin()
        {
            return RedirectToAction("RegisterAdmin", "Home");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterAdmin(Admins user)
        {
            ModelState.Remove("Id");
            if (ModelState.IsValid)
            {
                var existingAdmin = await _dbContext.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.username == user.username);
                if (existingAdmin != null)
                {
                    ModelState.AddModelError("username", "Username is already taken.");
                    ViewBag.Message = "Username is already taken.";
                    return View(user);
                }

                var newUser = new Users { name = user.username };
                _dbContext.Users.Add(newUser);
                await _dbContext.SaveChangesAsync();
                user.Id = newUser.Id;

                user.password = _passwordHasher.HashPassword(user.username, user.password);
                _dbContext.Admins.Add(user);
                await _dbContext.SaveChangesAsync();
                ViewBag.Message = "Admin data inserted successfully. Your AdminID: " + user.AdminId;
                return View(user);
            }
            else
            {
                ViewBag.Message = "There are some errors on the page";
                return View(user);
            }
        }

        [HttpGet]
        public IActionResult RegisterUser()
        {
            return RedirectToAction("UserPage", "Home");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterUser(Users user)
        {
            if (ModelState.IsValid)
            {
                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();
                ViewBag.Message = "Hello " + user.name + ", your Id is " + user.Id;
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
