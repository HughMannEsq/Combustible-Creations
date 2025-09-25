using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AutumnRidgeUSA.Models.Storage
{
    public class StorageContract
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Contract Number")]
        public string ContractNumber { get; set; } // e.g., "SC-2024-001"

        [Required]
        public int StorageUnitId { get; set; }

        [ForeignKey("StorageUnitId")]
        public virtual StorageUnit StorageUnit { get; set; }

        // Contract details
        [Required]
        [Display(Name = "Move-In Date")]
        public DateTime MoveInDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Gross Rent")]
        public decimal GrossRent { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Payment Cycle")]
        public string PaymentCycle { get; set; } // "Monthly", "Quarterly", "Annual"

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Security Deposit")]
        public decimal SecurityDeposit { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "SD Balance")]
        public decimal SecurityDepositBalance { get; set; }

        [Display(Name = "Online")]
        public bool IsOnline { get; set; } = false;

        [Display(Name = "Autopay")]
        public bool HasAutopay { get; set; } = false;

        // Contract status
        public bool IsActive { get; set; } = true;
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }

        // Audit fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<StorageContractUser> ContractUsers { get; set; } = new List<StorageContractUser>();
    }
}