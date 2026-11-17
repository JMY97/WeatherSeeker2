using System.ComponentModel.DataAnnotations;

namespace WebApp1.Models
{
    /// <summary>
    /// The account page: read-only details plus the delete confirmation. Kept
    /// separate from <see cref="Account"/> so the delete form binds only the
    /// password and never posts back the stored hash.
    /// </summary>
    public class AccountSettingsViewModel
    {
        public string Username { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        [Required(ErrorMessage = "Enter your password to confirm.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }
}
