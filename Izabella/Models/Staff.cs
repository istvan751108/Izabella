using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public enum StaffRole { Inszeminátor, Jelölő }

    public class Staff
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "A név megadása kötelező!")]
        [Display(Name = "Név")]
        public string Name { get; set; }

        [Display(Name = "Szerepkör")]
        public StaffRole Role { get; set; }

        [Display(Name = "Aktív")]
        public bool IsActive { get; set; } = true;
    }
}
