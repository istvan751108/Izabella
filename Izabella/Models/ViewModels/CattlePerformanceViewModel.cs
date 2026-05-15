namespace Izabella.Models.ViewModels
{
    public class CattlePerformanceViewModel
    {
        public string EarTag { get; set; }
        public List<LactationSummary> Lactations { get; set; } = new List<LactationSummary>();
        public double TotalLifeYield { get; set; }
        public double OverallDailyAverage { get; set; }
        public int BestLactationNumber { get; set; }
        public double BestLactationYield { get; set; }
    }

    public class LactationSummary
    {
        public int LactationNo { get; set; }
        public double TotalYield { get; set; }
        public double DailyAverage { get; set; }
        public int DaysInMilk { get; set; } // Hány napot fejt az adott laktációban
    }

    public class TopPerformanceViewModel
    {
        public List<CattleRankItem> TopByLactation { get; set; }
        public List<CattleRankItem> TopByLifetime { get; set; }
        public List<CattlePerformanceViewModel> AllCattlePerformances { get; set; } // ÚJ: Minden tehén adatai
    }

    public class CattleRankItem
    {
        public string EarTag { get; set; }
        public int? LactationNo { get; set; }
        public double TotalYield { get; set; }
        public double DailyAverage { get; set; }
    }
}
