using System.ComponentModel.DataAnnotations;
namespace WebApp1.Models
{
    public class Users
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public required string name { get; set; }

    }
}