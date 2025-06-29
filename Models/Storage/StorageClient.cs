namespace AutumnRidgeUSA.Models.Storage
{
    public class StorageClient : AutumnRidgeUSA.Models.Shared.Client
    {
        public float SecurityDeposit { get; set; }
        public float MonthlyPayment { get; set; }
        public string PaymentMethod { get; set; }
    }
}
