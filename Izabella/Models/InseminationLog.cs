using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class InseminationLog
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Állat fülszáma")]
        public string CattleEarTag { get; set; }

        [Required]
        [Display(Name = "Dátum")]
        public DateTime EventDate { get; set; } = DateTime.Now;

        [Required]
        public int BullSemenId { get; set; }
        public BullSemen BullSemen { get; set; }

        [Required]
        [Display(Name = "Inszeminátor")]
        public string InseminatorName { get; set; }

        [Display(Name = "Jelölő")]
        public string? MarkerName { get; set; }

        [Display(Name = "Rátermékenyítés?")]
        public bool IsReInsemination { get; set; } = false;

        public string? Note { get; set; }
    }
}