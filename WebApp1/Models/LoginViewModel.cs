using System.ComponentModel.DataAnnotations;

namespace WebApp1.Models
{
    public class LoginViewModel
    {

        public required string Username { get; set; }

        [DataType(DataType.Password)]
        public required string Password { get; set; }
    }
}
