using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Izabella.Models
{
    public class HeiferSalesBuffer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CattleId { get; set; }

        [ForeignKey("CattleId")]
        public virtual Cattle Cattle { get; set; }

        [Display(Name = "Pufferbe kerülés dátuma")]
        public DateTime AddedDate { get; set; } = DateTime.Now;

        [Display(Name = "Megjegyzés / Vevőjelölt")]
        public string? Notes { get; set; }
    }
}