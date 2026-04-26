namespace Izabella.Models
{
    public class WeightBuffer
    {
        public int Id { get; set; }
        public string EarTag { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public DateTime MeasuredDate { get; set; }
        public bool IsProcessed { get; set; } = false;
    }
}
