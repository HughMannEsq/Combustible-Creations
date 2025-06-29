using System.ComponentModel.DataAnnotations;

namespace AutumnRidgeUSA.Models.Shared
{
    public class Client
    {
        [Key]
        public string Raaid { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string Email { get; set; }
        public float Balance { get; set; }
        public DateTime SignupDate { get; set; }
    }
}