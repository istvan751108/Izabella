namespace Izabella.Models.ViewModels
{
    public class LivestockUnitReport
    {
        public string GroupName { get; set; } // Tenyészet vagy Tulajdonos neve
        public int CowCount { get; set; }
        public int Age0To6Count { get; set; }
        public int Age6To24Count { get; set; }
        public int Over24Count { get; set; }

        // Számított értékek
        public double TotalLivestockUnit =>
            (CowCount * 1.0) + (Age0To6Count * 0.4) + (Age6To24Count * 0.6) + (Over24Count * 1.0);

        public int TotalHeadCount => CowCount + Age0To6Count + Age6To24Count + Over24Count;
    }
}
