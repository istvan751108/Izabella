using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public enum SaleType
    {
        [Display(Name = "Vágás")] Slaughter = 1,
        [Display(Name = "Továbbtartás")] FurtherBreeding = 2,
        [Display(Name = "Export")] Export = 3,
        [Display(Name = "Tulajdonosváltás")] OwnershipChange = 4
    }
}
