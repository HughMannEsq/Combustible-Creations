namespace AutumnRidgeUSA.Models.Storage
{
    public class StorageContract : AutumnRidgeUSA.Models.Shared.Client
    {
        public string LockerId { get; set; } = string.Empty;
        public float SecurityDeposit { get; set; }
        public float MonthlyPayment { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
