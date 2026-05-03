namespace Izabella.Models.ViewModels
{
    public class MonthlyInventoryVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int? SelectedCompanyId { get; set; }
        public List<CompanyInventoryGroup> CompanyGroups { get; set; } = new();
        public List<Company> Companies { get; set; } = new();
    }

    public class CompanyInventoryGroup
    {
        public string CompanyName { get; set; }
        public List<string> EarTags { get; set; } = new();
    }
}