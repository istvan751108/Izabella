using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class DeathReason
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Elhullás oka")]
        public string Name { get; set; }
    }
}