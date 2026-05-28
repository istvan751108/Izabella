using System.Collections.Generic;

namespace Izabella.Models.ViewModels
{
    public class AnalysisRow
    {
        public string CategoryName { get; set; }
        public int Count { get; set; }
        public double AverageDaysInMilk { get; set; }

        // 🔥 Új oszlopok a PDF alapján:
        public double AverageCalvingCount { get; set; }   // Ell. ssz.

        public double AverageTestMilkCount { get; set; }  // pr.f ssz.
        public double AverageMilkDeviation { get; set; }  // Eltérés (kg)
        public double AverageConditionScore { get; set; } // Kondipont

        // Beltartalmi mutatók
        public double AverageMilkKg { get; set; }

        public double AverageFatPercentage { get; set; }
        public double AverageProteinPercentage { get; set; }
        public double AverageSugarPercentage { get; set; }
        public double AverageSomaticCell { get; set; }
        public double AverageUrea { get; set; }
    }

    public class CompleteAnalysisReport
    {
        public string CompanyName { get; set; }
        public string PeriodText { get; set; } // Pl: "2026.05" vagy "2026 Teljes év"
        public List<AnalysisRow> LactationDayRows { get; set; } = new List<AnalysisRow>();
        public List<AnalysisRow> MilkVolumeRows { get; set; } = new List<AnalysisRow>();
    }
}