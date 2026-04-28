namespace Izabella.Models
{
    public class AbortionReportViewModel
    {
        public string EarTag { get; set; }
        public DateTime AbortionDate { get; set; }
        public int AbortionCount { get; set; } // Hanyadik vetélése az állatnak
        public string Comment { get; set; }
    }

    public class ExpectedCalvingViewModel
    {
        public string EarTag { get; set; }
        public DateTime LastInseminationDate { get; set; }
        public DateTime ExpectedDate { get; set; }
        public int DaysSinceInsem { get; set; }
        public bool IsOverdue => DaysSinceInsem > 300; // Túlhordott-e
        public string StatusColor => DaysSinceInsem > 300 ? "table-danger" : (DaysSinceInsem > 285 ? "table-warning" : "");
    }
}
