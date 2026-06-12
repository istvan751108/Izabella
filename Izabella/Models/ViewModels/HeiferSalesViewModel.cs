using System;

namespace Izabella.ViewModels
{
    public class HeiferSalesViewModel
    {
        public int CattleId { get; set; }
        public string EarTag { get; set; }
        public string EnarNumber { get; set; }
        public DateTime BirthDate { get; set; }
        public DateTime? LastInseminationDate { get; set; }
        public string InseminationBullKlsz { get; set; }
        public string FatherKlsz { get; set; }
        public string MotherEnar { get; set; }

        // Anya - 1. laktáció
        public double DamLact1Tej { get; set; }

        public double DamLact1Zsir { get; set; }
        public double DamLact1Feherje { get; set; }
        public double DamLact1Sejt { get; set; }

        // Anya - Legjobb laktáció
        public double DamBestLactTej { get; set; }

        public double DamBestLactZsir { get; set; }
        public double DamBestLactFeherje { get; set; }

        // Anyai Nagyanya - 1. laktáció
        public double GrandDamLact1Tej { get; set; }

        public double GrandDamLact1Zsir { get; set; }
        public double GrandDamLact1Feherje { get; set; }
        public double GrandDamLact1Sejt { get; set; }

        // Anyai Nagyanya - Legjobb laktáció
        public double GrandDamBestLactTej { get; set; }

        public double GrandDamBestLactZsir { get; set; }
        public double GrandDamBestLactFeherje { get; set; }

        public bool IsInBuffer { get; set; }
    }
}