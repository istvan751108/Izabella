using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Izabella.Models
{
    public class Customer
    {
        public int Id { get; set; }
        [Required, Display(Name = "Vásárló neve / Cég")]
        public string Name { get; set; }
        [Display(Name = "Cím")]
        public string Address { get; set; }
        [Display(Name = "Adószám / Adóazonosító")]
        public string TaxNumber { get; set; }
        [Display(Name = "Telefonszám")]
        public string Phone { get; set; }
        [EmailAddress]
        public string Email { get; set; }

        // Kapcsolat az eladásokkal
        [ValidateNever]
        public virtual ICollection<SaleTransaction> Sales { get; set; }
    }
}
