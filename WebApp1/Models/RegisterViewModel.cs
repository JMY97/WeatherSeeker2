using System.ComponentModel.DataAnnotations;

namespace WebApp1.Models
{
    /// <summary>
    /// Sign-up form. Separate from <see cref="Account"/> so a caller cannot bind
    /// <c>PasswordHash</c>, <c>Id</c>, or <c>CreatedAt</c> through the body.
    /// </summary>
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Choose a username.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Use 3 to 100 characters.")]
        [RegularExpression(@"^[\w.@+-]+$", ErrorMessage = "Letters, digits, and . @ + - only.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose a password.")]
        [StringLength(256, MinimumLength = 12, ErrorMessage = "Use a password of 12 to 256 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repeat your password.")]
        [StringLength(256, ErrorMessage = "Use a password of 12 to 256 characters.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
