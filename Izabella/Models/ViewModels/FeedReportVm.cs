namespace Izabella.Models.ViewModels
{
    public class FeedReportVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int DaysInMonth { get; set; }
        public int? SelectedCompanyId { get; set; }
        public List<Company> Companies { get; set; } = new();

        // Kulcs: Korcsoport neve, Érték: Lista a napok adataival (1. nap, 2. nap...)
        public Dictionary<string, List<DayStat>> RowData { get; set; } = new();
        public List<string> AgeGroups { get; set; } = new()
        {
            "Itatásos borjú", "Borjú", "Növendék 6-9", "Növendék 9-12",
            "Növendék 12 hó-tól", "Vemhes üsző", "Tehén"
        };
    }

    public class DayStat
    {
        public int Count { get; set; }
        public double TotalWeight { get; set; }
    }
}