namespace Izabella.Models
{
    public class DailyStat
    {
        public int Id { get; set; }
        public DateTime StatDate { get; set; }
        public int CompanyId { get; set; }
        public string AgeGroup { get; set; }
        public int Count { get; set; }        // Aznap végi darabszám
        public double TotalWeight { get; set; } // Aznap végi összsúly
    }
}
