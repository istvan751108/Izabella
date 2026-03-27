using System;
using System.Collections.Generic;

namespace Izabella.Models.ViewModels
{
    public class DailyReportVm
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public double LiquidTotal { get; set; }
        public double SolidTotal { get; set; }
        public List<SolidManureLoad> Loads { get; set; } = new();

        public List<LiquidManure> Liquids { get; set; } = new List<LiquidManure>();
        public List<SolidManureDaily> Days { get; set; } = new List<SolidManureDaily>();

        // Ha kell dátum a view-nak
        public DateTime Date { get; set; }
        public double YearlyLiquid { get; set; }
        public double YearlySolid { get; set; }
    }
}
