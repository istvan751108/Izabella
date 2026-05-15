using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class MilkProduction
    {
        public int Id { get; set; }

        // Összekötés a Cattle táblával
        public int CattleId { get; set; }
        public virtual Cattle Cattle { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        public int LactationNo { get; set; }      // D oszlop az Afimilkből
        public int DaysInMilk { get; set; }       // E oszlop az Afimilkből

        // Tej adatok (kg)
        public double Yield1 { get; set; }
        public double Yield2 { get; set; }
        public double Yield3 { get; set; }
        public double TotalYield => Yield1 + Yield2 + Yield3;

        // Beltartalom (%)
        public double? Fat1 { get; set; }
        public double? Fat2 { get; set; }
        public double? Fat3 { get; set; }
        public double? Protein1 { get; set; }
        public double? Protein2 { get; set; }
        public double? Protein3 { get; set; }

        // Jelző, ha ez egy "becsült/pótolt" adat (műszaki hiba vagy elhagyott csat miatt)
        public bool IsEstimated { get; set; } = false;
        public string? Comment { get; set; }
    }
}
