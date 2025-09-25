// Models/Storage/StorageUnit.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutumnRidgeUSA.Models.Storage
{
    public class StorageUnit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Unit/Locker ID")]
        public string UnitId { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Unit Size")]
        public string UnitSize { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Base Rent")]
        public decimal BaseRent { get; set; }

        [StringLength(100)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<StorageContract> Contracts { get; set; } = new List<StorageContract>();
    }
}