namespace Izabella.Models.ViewModels
{
    public class InseminationStatsViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<MarkerStatItem> MarkerStats { get; set; } = new();

        // Összesített értékek a kártyákhoz
        public int TotalInsem { get; set; }
        public int TotalPreg { get; set; }
        public int CowInsem { get; set; }
        public int CowPreg { get; set; }
        public int HeiferInsem { get; set; }
        public int HeiferPreg { get; set; }

        public double TotalIndex => TotalPreg > 0 ? (double)TotalInsem / TotalPreg : 0;
        public double CowIndex => CowPreg > 0 ? (double)CowInsem / CowPreg : 0;
        public double HeiferIndex => HeiferPreg > 0 ? (double)HeiferInsem / HeiferPreg : 0;
    }

    public class MarkerStatItem
    {
        public string MarkerName { get; set; }

        public int CowInsem { get; set; }
        public int CowPreg { get; set; }
        public int CowPending { get; set; } // Hozzáadva
        public double CowIndex => CowPreg > 0 ? (double)CowInsem / CowPreg : 0;

        public int HeiferInsem { get; set; }
        public int HeiferPreg { get; set; }
        public int HeiferPending { get; set; } // Hozzáadva
        public double HeiferIndex => HeiferPreg > 0 ? (double)HeiferInsem / HeiferPreg : 0;

        public int TotalInsem => CowInsem + HeiferInsem;
        public int TotalPreg => CowPreg + HeiferPreg;
        public int TotalPending => CowPending + HeiferPending; // Hozzáadva
        public double TotalIndex => TotalPreg > 0 ? (double)TotalInsem / TotalPreg : 0;
    }
}
