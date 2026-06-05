using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Izabella.Models.ViewModels
{
    // A fő ViewModel, amit a View (és a Controller GET/POST) használ
    public class MastitisTreatmentViewModel
    {
        [Required(ErrorMessage = "A tehén kiválasztása kötelező!")]
        public int CattleId { get; set; }

        [Required(ErrorMessage = "A diagnózis megadása kötelező!")]
        [Display(Name = "Diagnózis / Tünet")]
        public string Diagnosis { get; set; }

        // Itt van a lista, amit hiányolt a Controller
        public List<MastitisTreatmentRowViewModel> Rows { get; set; } = new List<MastitisTreatmentRowViewModel>();
    }

    // EZT HIÁNYOLJA A FORDÍTÓ (Ez képvisel egy sort a kezelési táblázatban)
    public class MastitisTreatmentRowViewModel
    {
        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "A negyed megadása kötelező!")]
        public string Quarter { get; set; } // Pl: JE, BE, JH, BH

        [Required(ErrorMessage = "A gyógyszer kiválasztása kötelező!")]
        public int MedicationId { get; set; }

        public int? SecondaryMedicationId { get; set; } // Opcionális kombináció (pl. Eurofit)

        public string Note { get; set; } // Megjegyzés soronként
    }
}