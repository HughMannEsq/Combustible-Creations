using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutumnRidgeUSA.Models.Storage
{
    public class StorageContract
    {
        [Key]
        public int Id { get; set; }

        // Foreign key to Users table
        [Required]
        public int UserId { get; set; }

        // Navigation property
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        // Storage contract specific fields
        [StringLength(50)]
        public string? LockerId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal SecurityDeposit { get; set; }

        [StringLength(50)]
        public string? SecurityDepositReceived { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal MonthlyPayment { get; set; }

        [StringLength(50)]
        public string? PaymentMethod { get; set; }

        // Audit fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Status fields
        public bool IsActive { get; set; } = true;
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
    }
}