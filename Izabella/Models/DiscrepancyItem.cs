namespace Izabella.Models
{
    public class DiscrepancyItem
    {
        public string EarTag { get; set; }
        public string IzabellaStatus { get; set; }
        public string AfimilkStatus { get; set; }
        public string DiscrepancyType { get; set; } // "MissingFromAfi" vagy "UnexpectedInAfi"
        public string Message { get; set; }
    }
}