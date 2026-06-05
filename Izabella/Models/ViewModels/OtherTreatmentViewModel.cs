using System;
using System.ComponentModel.DataAnnotations;

namespace Izabella.Models.ViewModels
{
    public class OtherTreatmentViewModel
    {
        [Required(ErrorMessage = "A tehén kiválasztása kötelező!")]
        [Display(Name = "Kezelt állat")]
        public int CattleId { get; set; }

        [Required(ErrorMessage = "A dátum megadása kötelező!")]
        [DataType(DataType.Date)]
        [Display(Name = "Kezelés dátuma")]
        public DateTime TreatmentDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "A diagnózis megadása kötelező!")]
        [Display(Name = "Diagnózis / Betegség")]
        public OtherTreatmentType TreatmentType { get; set; }

        [Required(ErrorMessage = "Az elsődleges gyógyszer kiválasztása kötelező!")]
        [Display(Name = "Elsődleges gyógyszer")]
        public int MedicationId { get; set; }

        [Required(ErrorMessage = "A beadott dózis megadása kötelező!")]
        [Range(0.1, 1000, ErrorMessage = "A dózisnak 0-nál nagyobbnak kell lennie!")]
        [Display(Name = "Beadott mennyiség (dózis)")]
        public double AdministeredDose { get; set; }

        [Display(Name = "Másodlagos / Támogató gyógyszer (opcionális)")]
        public int? SecondaryMedicationId { get; set; }

        [Display(Name = "Másodlagos gyógyszer dózisa")]
        public double? SecondaryAdministeredDose { get; set; }

        [Display(Name = "Megjegyzés")]
        public string? Note { get; set; }
    }
}