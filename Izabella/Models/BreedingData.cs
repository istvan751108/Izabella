using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class BreedingData
    {
        [Key]
        public int Id { get; set; }
        public int CattleId { get; set; } // Idegen kulcs

        [DataType(DataType.Date)]
        public DateTime? LastInseminationDate { get; set; }

        public string? SireKlsz { get; set; } // Párosított bika

        [DataType(DataType.Date)]
        public DateTime? PregnancyTestDate { get; set; }

        public bool? IsPregnant { get; set; }

        [DataType(DataType.Date)]
        public DateTime? AbortionDate { get; set; }
    }
}
