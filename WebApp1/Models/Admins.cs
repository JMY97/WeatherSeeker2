using System.ComponentModel.DataAnnotations;

namespace WebApp1.Models
{
    public class Admins
    {
        [Key]
        public int AdminId { get; set; }

        public int Id { get; set; }

        [Required(ErrorMessage = "Choose a username.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Use 3 to 100 characters.")]
        [RegularExpression(@"^[\w.@+-]+$", ErrorMessage = "Letters, digits, and . @ + - only.")]
        [Display(Name = "Username")]
        public string username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose a password.")]
        [StringLength(256, MinimumLength = 12, ErrorMessage = "Use at least 12 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string password { get; set; } = string.Empty;
    }
}
