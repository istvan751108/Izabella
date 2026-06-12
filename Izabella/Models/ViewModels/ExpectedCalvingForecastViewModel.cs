using System;

namespace Izabella.ViewModels
{
    public class ExpectedCalvingForecastViewModel
    {
        public string MonthLabel { get; set; }        // pl. "2026.07"
        public int TotalExpectedCalvings { get; set; } // Összes várható ellés
        public int HeiferCalvings { get; set; }        // Ebből vemhes üsző ellése
        public int CowCalvings { get; set; }           // Ebből tehén ellése
        public int RemovedViaBuffer { get; set; }      // Mennyi üszőt adunk el innen (pufferben van)
        public int NetRemainingCalvings => TotalExpectedCalvings - RemovedViaBuffer; // Amennyi nálunk marad
    }
}