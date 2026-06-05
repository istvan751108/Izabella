using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Izabella.Models.ViewModels
{
    public class ReproTreatmentViewModel
    {
        [Required(ErrorMessage = "A kezdő dátum megadása kötelező!")]
        [DataType(DataType.Date)]
        [Display(Name = "Protokoll kezdete / Kezelés dátuma")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "A kezelés típusának kiválasztása kötelező!")]
        [Display(Name = "Szaporodásbiológiai kezelés típusa")]
        public ReproTreatmentType TreatmentType { get; set; }

        // Egyedi kezelés esetén használt mező
        [Display(Name = "Kiválasztott állat (Egyedi kezelésnél)")]
        public int? SingleCattleId { get; set; }

        // Tömeges kezelésnél a kijelölt tehenek ID-jai
        [Display(Name = "Kezelésre kijelölt tehenek")]
        public List<int> SelectedCattleIds { get; set; } = new List<int>();

        [Display(Name = "Megjegyzés")]
        public string? Note { get; set; }
    }
}