namespace Izabella.Models
{
    public class LiquidManureSplit
    {
        public int Id { get; set; }

        public int LiquidManureId { get; set; }
        public LiquidManure LiquidManure { get; set; }

        public double Amount { get; set; }

        public string VoucherNumber { get; set; }
    }
}
