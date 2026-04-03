using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class Company
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "A {0} mező kitöltése kötelező.")]
        [Display(Name = "Cégnév")]
        public string Name { get; set; } // pl. Bátortrade Kft.
    }
}
