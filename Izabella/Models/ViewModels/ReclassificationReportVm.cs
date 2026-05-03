namespace Izabella.Models.ViewModels
{
    public class ReclassificationReportVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int? SelectedCompanyId { get; set; }
        public List<CompanyReclassGroup> CompanyGroups { get; set; } = new();
        public List<Company> Companies { get; set; } = new();
    }

    public class CompanyReclassGroup
    {
        public string CompanyName { get; set; }
        public List<ReclassificationItem> Items { get; set; } = new();
    }

    public class ReclassificationItem
    {
        public string EarTag { get; set; }
        public string EnarNumber { get; set; }
        public DateTime CalvingDate { get; set; }
        public string Code => "Átminősítés";
    }
}