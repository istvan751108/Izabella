using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class BulkBcsViewModel
    {
        public int CattleId { get; set; }
        public string EarTag { get; set; } = string.Empty;
        public string AgeGroup { get; set; } = string.Empty;
        public string? Stall { get; set; }

        [Range(1.0, 5.0, ErrorMessage = "A kondíciópontnak 1 és 5 között kell lennie!")]
        public double BodyConditionScore { get; set; } // decimal helyett double
    }
}