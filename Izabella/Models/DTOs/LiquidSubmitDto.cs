namespace Izabella.Models.DTOs
{
    public class LiquidSubmitDto
    { 
        public DateTime Date { get; set; } 
        public double TotalAmount { get; set; } 
        public List<LiquidSplitDto> Splits { get; set; } 
    }
}
