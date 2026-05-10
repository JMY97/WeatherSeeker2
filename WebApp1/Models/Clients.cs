using System.ComponentModel.DataAnnotations;
namespace WebApp1.Models
{
    public class Clients
    {
        [Key]
        public int ClientId { get; set; }
        public int Id { get; set; }
        [Required]
        public required string username { get; set; }

        public required string password { get; set; }


    }
}