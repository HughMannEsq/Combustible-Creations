// Models/UserDivision.cs - Junction table for many-to-many relationship
using System.ComponentModel.DataAnnotations;

namespace AutumnRidgeUSA.Models
{
    public class UserDivision
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int DivisionId { get; set; }
        public Division Division { get; set; } = null!;

        public DateTime ContractedDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}