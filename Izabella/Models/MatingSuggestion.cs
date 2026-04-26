using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class MatingSuggestion
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Tehén fülszáma")]
        public string CattleEarTag { get; set; }

        [Required]
        [Display(Name = "Javasolt Bika KLSZ")]
        public string SuggestedKlsz { get; set; }

        [Display(Name = "Bika neve")]
        public string? SuggestedBullName { get; set; } // Opcionális, de segít a beazonosításban

        [Display(Name = "Prioritás")]
        [Range(1, 3)]
        public int Priority { get; set; } = 1; // 1: Elsődleges, 2: Tartalék, stb.

        [Display(Name = "Terv dátuma")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}