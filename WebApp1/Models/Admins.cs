using System.ComponentModel.DataAnnotations;
namespace WebApp1.Models
{
    public class Admins
    {
        [Key]
        public int AdminId { get; set; }
        [Required]
        public int Id { get; set; }
        [Required]
        public required string username { get; set; }
        
        public required string password { get; set; }
        

    }
}