using System.ComponentModel.DataAnnotations;

namespace WebApp1.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Enter your username.")]
        [StringLength(100)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your password.")]
        [StringLength(256)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }
}
