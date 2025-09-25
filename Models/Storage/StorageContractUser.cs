// Models/Storage/StorageContractUser.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutumnRidgeUSA.Models.Storage
{
    public class StorageContractUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StorageContractId { get; set; }

        [ForeignKey("StorageContractId")]
        public virtual StorageContract StorageContract { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [Display(Name = "Primary Contract Holder")]
        public bool IsPrimaryContractHolder { get; set; } = false;

        [Display(Name = "Access Level")]
        [StringLength(20)]
        public string AccessLevel { get; set; } = "Full"; // "Full", "ReadOnly", "Emergency"

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RemovedAt { get; set; }
    }
}