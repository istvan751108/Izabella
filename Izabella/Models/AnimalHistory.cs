namespace Izabella.Models
{
    public class AnimalHistory
    {
        public int Id { get; set; }
        public int CattleId { get; set; }
        public Cattle Cattle { get; set; }

        public DateTime EventDate { get; set; }

        // Súlyadatok
        public double Weight { get; set; }
        public double WeightGain { get; set; } // Az előző mérés óta eltelt hízás

        // Korcsoport változás (honnan - hová)
        public string? OldAgeGroup { get; set; }
        public string? NewAgeGroup { get; set; }

        // Helyváltoztatás
        public int? OldHerdId { get; set; }
        public int? NewHerdId { get; set; }
        public string? StallName { get; set; } // Istálló/Box neve vagy száma

        public string? Type { get; set; } // "Súlymérés", "Korosbítás", "Áthelyezés"
        public bool IsEnarReported { get; set; } = false;
    }
}
