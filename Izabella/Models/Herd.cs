using Izabella.Models;
using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class Herd
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "A {0} mező kitöltése kötelező.")]
        [Display(Name = "Tenyészet neve")]
        public string Name { get; set; }

        [Required(ErrorMessage = "A {0} mező kitöltése kötelező.")]
        [Display(Name = "Tenyészetkód")]
        public string HerdCode { get; set; }

        [Display(Name = "Cég")]
        public int CompanyId { get; set; } // Kié a tenyészet?

        [Display(Name = "Cégnév")]
        public virtual Company? Company { get; set; }

        // Opcionális mezők: csak ott töltjük ki, ahol van ellés
        [Display(Name = "Alapértelmezett betűjel")]
        public string? DefaultPrefix { get; set; }

        [Display(Name = "ENAR Prefix")]
        public string? EnarPrefix { get; set; }
    }
}

