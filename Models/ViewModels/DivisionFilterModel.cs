namespace AutumnRidgeUSA.Models.ViewModels
{
    public class DivisionFilterModel
    {
        public List<int> SelectedDivisionIds { get; set; } = new List<int>();
        public string FilterOperator { get; set; } = "OR"; // "AND" or "OR"
        public List<Division> AvailableDivisions { get; set; } = new List<Division>();
    }
}