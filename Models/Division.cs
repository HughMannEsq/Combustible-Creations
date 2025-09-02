// Models/Division.cs
using System.ComponentModel.DataAnnotations;

namespace AutumnRidgeUSA.Models
{
    public class Division
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation property
        public ICollection<UserDivision> UserDivisions { get; set; } = new List<UserDivision>();
    }
}