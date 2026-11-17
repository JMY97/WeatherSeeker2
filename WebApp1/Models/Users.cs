using System.ComponentModel.DataAnnotations;

namespace WebApp1.Models
{
    public class Users
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        [Display(Name = "Name")]
        public string name { get; set; } = string.Empty;
    }
}
