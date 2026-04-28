namespace Izabella.Models.ViewModels
{
    public class SemenUsageReportViewModel
    {
        public string Klsz { get; set; }
        public string BullName { get; set; }
        public int InseminationCount { get; set; } // Normál termékenyítés
        public int ReInseminationCount { get; set; } // Rátermékenyítés
        public int ScrapCount { get; set; } // Selejt
        public int TotalUsage => InseminationCount + ReInseminationCount + ScrapCount;
    }
}
