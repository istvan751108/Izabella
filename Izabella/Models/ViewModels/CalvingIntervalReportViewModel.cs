using System;
using System.Collections.Generic;

namespace Izabella.Models.ViewModels
{
    public class CalvingIntervalReportViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public double AverageIntervalDays { get; set; }
        public int TotalCalvingsInMonth { get; set; } // Összes ellés a hónapban
        public int MultiParousCalvingsCount { get; set; } // Ebből a többször ellett tehenek száma (akiknél van KEI)

        // Részletes lista a táblázathoz
        public List<CalvingIntervalDetailsItem> Details { get; set; } = new List<CalvingIntervalDetailsItem>();
    }

    public class CalvingIntervalDetailsItem
    {
        public string EarTag { get; set; }
        public string EnarNumber { get; set; }
        public DateTime CalvingDate { get; set; }
        public int LactationNo { get; set; }
        public int IntervalDays { get; set; }
    }
}