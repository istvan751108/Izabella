using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class MilkDataStaging
    {
        public int Id { get; set; }

        [Display(Name = "Adatnap")]
        [DataType(DataType.Date)]
        public DateTime RecordDate { get; set; } // A tényleges fejési nap (fájlnév dátuma - 1 nap)

        [Display(Name = "Importálás ideje")]
        public DateTime ImportTimestamp { get; set; } // Mikor került be a rendszerbe

        // Afimilk azonosítók
        public string EarTag { get; set; }       // Pl: L0015
        public string BarnId { get; set; }       // Istálló
        public int LactationNo { get; set; }     // Hanyadik laktáció
        public int DaysInMilk { get; set; }      // Laktációs napok
        public string Enar { get; set; }         // Regisztrációs szám

        // Tejmennyiségek (kg)
        public double? Yield1 { get; set; }
        public double? Yield2 { get; set; }
        public double? Yield3 { get; set; }

        // Analitika (%)
        public double? Fat1 { get; set; }
        public double? Fat2 { get; set; }
        public double? Fat3 { get; set; }
        public double? Protein1 { get; set; }
        public double? Protein2 { get; set; }
        public double? Protein3 { get; set; }

        public bool IsProcessed { get; set; } = false; // Feldolgoztuk már a végleges táblákba?
    }
}
