using System;
using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class MilkQualityLab
    {
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Vizsgálat dátuma")]
        public DateTime RecordDate { get; set; }

        public int DekadNumber { get; set; }

        [Display(Name = "Zsír %")]
        public double FatPercentage { get; set; }

        [Display(Name = "Fehérje %")]
        public double ProteinPercentage { get; set; }

        // ÁTÍRVA STRING-RE
        [Display(Name = "Szomatikus Sejtszám")]
        public string? SomaticCellCount { get; set; }

        // ÁTÍRVA STRING-RE
        [Display(Name = "Csíraszám")]
        public string? BacteriaCount { get; set; }
    }
}