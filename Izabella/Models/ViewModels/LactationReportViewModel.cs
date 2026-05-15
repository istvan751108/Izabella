namespace Izabella.Models.ViewModels
{
    public class LactationReportViewModel
    {
        public DateTime SelectedMonth { get; set; }
        public List<LactationGroupData> Groups { get; set; } = new List<LactationGroupData>();
        public int TotalCows { get; set; }
        public bool IsYearlyView { get; set; }
    }

    public class LactationGroupData
    {
        public int LactationNumber { get; set; } // 1, 2, 3...
        public int Count { get; set; }           // Hány tehén van ebben a csoportban
        public double Percentage { get; set; }   // %-os arány
        public double AvgDailyYield { get; set; } // Az adott csoport átlagos napi termelése
    }
}
