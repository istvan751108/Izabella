namespace Izabella.Models.ViewModels
{
    public class AfiExportViewModel
    {
        public int CattleId { get; set; }
        public string EarTag { get; set; } = string.Empty;
        public string EnarNumber { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public string AgeGroup { get; set; } = string.Empty;
        public string Stall { get; set; } = string.Empty;
        public bool IsSelected { get; set; } // A checklist pipálásához
    }
}