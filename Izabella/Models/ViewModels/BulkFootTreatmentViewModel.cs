using System;
using System.ComponentModel.DataAnnotations;

namespace Izabella.Models.ViewModels
{
    public class BulkFootTreatmentViewModel
    {
        [Required(ErrorMessage = "A dátum megadása kötelező!")]
        [DataType(DataType.Date)]
        public DateTime TreatmentDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "A kezelés típusának kiválasztása kötelező!")]
        public FootTreatmentType TreatmentType { get; set; }

        // Ide jön a trükk: a felhasználó ide másolja be az 1000 fülnyitót vágólapról
        [Required(ErrorMessage = "Kérjük, adjon meg legalább egy fül- vagy ENAR számot!")]
        [Display(Name = "Fül- vagy ENAR számok listája")]
        public string RawEarTags { get; set; }

        [Display(Name = "Megjegyzés / Alkalmazott szer (pl. Rézgálic 5%)")]
        public string Note { get; set; }
    }
}