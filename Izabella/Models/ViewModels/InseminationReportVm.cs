namespace Izabella.Models.ViewModels
{
    public class InseminationReportVm
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? SelectedCompanyId { get; set; }
        public List<InseminationReportEntry> Entries { get; set; } = new();
    }

    public class InseminationReportEntry
    {
        public string EnarNumber { get; set; }
        public DateTime InseminationDate { get; set; }
        public string Klsz { get; set; }
        public string BullName { get; set; }
        public string Method { get; set; } = "1";
        public string InseminatorCode { get; set; }
        public string SemenBatchNumber { get; set; }
        public string SemenType { get; set; }
        public string SemenOrigin { get; set; }
    }    
}
