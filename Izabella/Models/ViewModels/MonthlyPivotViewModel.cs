namespace Izabella.Models.ViewModels
{
    public enum MilkReportType { Yield, Fat, Protein }
    public class MonthlyPivotViewModel
    {
        public MilkReportType ReportType { get; set; }
        public DateTime SelectedMonth { get; set; }
        public List<int> DaysInMonth { get; set; }
        public List<CattleMilkRow> Rows { get; set; }
        public Dictionary<int, DailyTotal> DailyTotals { get; set; }
    }

    public class CattleMilkRow
    {
        public string EarTag { get; set; }
        public Dictionary<int, DayProduction> DailyData { get; set; } // Kulcs: nap (1-31)
        public double RowAverage => DailyData.Values.Any(v => v.HasData)
            ? DailyData.Values.Where(v => v.HasData).Average(v => v.Total) : 0;
    }

    public class DayProduction
    {
        public double Y1 { get; set; }
        public double Y2 { get; set; }
        public double Y3 { get; set; }
        public double Total => Y1 + Y2 + Y3;
        public bool HasData { get; set; }
    }

    public class DailyTotal
    {
        public double SumY1 { get; set; }
        public double SumY2 { get; set; }
        public double SumY3 { get; set; }
        public double DayTotal => SumY1 + SumY2 + SumY3;
    }
}
