using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public enum Gender { Bika, Üsző }
    public enum ExitType { Vágóhíd, Továbbtartás, Export, Elhullás }

    public class Cattle
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [RegularExpression(@"^[A-Z]{1}\d{4}$", ErrorMessage = "A fülszám formátuma: 1 betű + 4 számjegy (pl. A1234)")]
        [Display(Name = "Fülszám")]
        public string EarTag { get; set; }

        [Required]
        [RegularExpression(@"^[A-Z]{2}\d{10}$", ErrorMessage = "Az ENAR formátuma: 2 betű + 10 számjegy (pl. HU1234567890)")]
        [Display(Name = "ENAR szám")]
        public string EnarNumber { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Marhalevél szám")]
        public string PassportNumber { get; set; }

        [Display(Name = "Marhalevél sorszám")]
        public int PassportSequence { get; set; } = 1;

        [Display(Name = "Tulajdonos (Cég)")]
        public int CompanyId { get; set; }
        [Display(Name = "Cég")]
        public virtual Company? Company { get; set; }

        [Display(Name = "Aktuális Tenyészet")]
        public int CurrentHerdId { get; set; }
        [Display(Name = "Tenyészet kód")]
        public virtual Herd? CurrentHerd { get; set; }

        [Display(Name = "Korcsoport")]
        public string AgeGroup { get; set; } // pl. "0-3 hó", "Vemhes üsző", "Tehén"

        [Display(Name = "Ikerellés?")]
        public bool IsTwin { get; set; } = false;

        [Display(Name = "Élve született?")]
        public bool IsAlive { get; set; } = true;

        [Display(Name = "Anya kora elléskor")]
        public string? DamAgeAtCalving { get; set; } // "Üsző" vagy "Tehén"

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Születési idő")]
        public DateTime BirthDate { get; set; }

        [Display(Name = "Születési súly (kg)")]
        public double BirthWeight { get; set; }

        [Required]
        [Display(Name = "Nem")]
        public Gender Gender { get; set; }

        [Display(Name = "Anya ENAR")]
        public string? MotherEnar { get; set; }

        [Display(Name = "Apa KLSZ (Sperma)")]
        public string? FatherKlsz { get; set; }

        // --- Üsző specifikus adatok (Navigációs tulajdonság) ---
        public virtual BreedingData? Breeding { get; set; }

        [Display(Name = "Kikerülés dátuma")]
        [DataType(DataType.Date)]
        public DateTime? ExitDate { get; set; }
        
        [Display(Name = "Kikerülés típusa")]
        public ExitType? ExitType { get; set; }

        [Display(Name = "Aktív?")]
        public bool IsActive { get; set; } = true;
    }
}
