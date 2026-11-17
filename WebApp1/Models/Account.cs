using System.ComponentModel.DataAnnotations;

namespace WebApp1.Models
{
    /// <summary>
    /// A single account. Every account can sign in, read the console, and delete
    /// itself. There are no roles: all accounts are equal.
    /// </summary>
    public class Account
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        [RegularExpression(@"^[\w.@+-]+$")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(512)]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Rotated on sign-out so a cookie captured earlier stops working
        /// immediately instead of staying valid until natural expiry. The auth
        /// cookie is otherwise self-contained and unforgeable-but-permanent.
        /// </summary>
        public Guid SessionVersion { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Usernames are stored lowercase so sign-up, sign-in, and the unique
        /// index all agree. Without this, "Ada" and "ada" both register and then
        /// sign-in resolves whichever row the database returns first.
        /// </summary>
        public static string Normalize(string username) => username.Trim().ToLowerInvariant();
    }
}
