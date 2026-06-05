using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class AnimalTreatment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Állat")]
        public int CattleId { get; set; }

        public virtual Cattle? Cattle { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Kezelés dátuma")]
        public DateTime TreatmentDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Kezelés típusa")]
        public TreatmentCategory Category { get; set; } // Tőgy, Láb, Szaporodás, Egyéb

        [Required]
        [Display(Name = "Felhasznált gyógyszer")]
        public int MedicationId { get; set; }

        public virtual Medication? Medication { get; set; }

        [Required]
        [Range(0.1, 999.9, ErrorMessage = "Az adagolásnak nagyobbnak kell lennie mint 0!")]
        [Display(Name = "Beadott mennyiség")]
        public double AdministeredDose { get; set; }

        [Display(Name = "Tej várakozási idő lejárta")]
        [DataType(DataType.Date)]
        public DateTime? MilkWithdrawalExpiry { get; set; }

        [Display(Name = "Hús várakozási idő lejárta")]
        [DataType(DataType.Date)]
        public DateTime? MeatWithdrawalExpiry { get; set; }

        [StringLength(500)]
        [Display(Name = "Diagnózis / Megjegyzés")]
        public string? Note { get; set; }
    }
}