using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class Company
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "A {0} mező kitöltése kötelező.")]
        [Display(Name = "Cégnév")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Az Ügyfél-azonosító kitöltése kötelező.")]
        [Display(Name = "Ügyfél-azonosító (MÁK)")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Az azonosítónak 10 számjegyből kell állnia.")]
        public string ClientId { get; set; } // pl. 1001798252
    }
}