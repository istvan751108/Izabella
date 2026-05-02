namespace Izabella.Models.ViewModels
{
    public class ExitReportVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int? SelectedCompanyId { get; set; } // Új: szűréshez
        public List<CompanyExitGroup> CompanyGroups { get; set; } = new();
        public List<Company> Companies { get; set; } = new(); // Új: a lenyílóhoz
    }

    public class CompanyExitGroup
    {
        public string CompanyName { get; set; }
        public List<Cattle> ExitedCattle { get; set; }
    }
}
