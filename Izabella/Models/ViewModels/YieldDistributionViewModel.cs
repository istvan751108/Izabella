namespace Izabella.Models.ViewModels
{
    public class YieldDistributionViewModel
    {
        public int Year { get; set; }
        public List<string> Months { get; set; } = new List<string>();
        public List<YieldBracketGroup> FirstLactationGroups { get; set; } = new List<YieldBracketGroup>();
        public List<YieldBracketGroup> MultiLactationGroups { get; set; } = new List<YieldBracketGroup>();
        public List<int> TotalCowsByMonth { get; set; } = new List<int>();
    }

    public class YieldBracketGroup
    {
        public string Bracket { get; set; } // Pl: "41-50"
        public List<int> MonthlyCounts { get; set; } = new List<int>(); // Január: 194, Február: 197...
    }
}
