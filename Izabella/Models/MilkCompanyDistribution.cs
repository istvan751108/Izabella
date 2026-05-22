using System;
using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class MilkCompanyDistribution
    {
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime DistributionDate { get; set; }

        [Required]
        public int CompanyId { get; set; }

        public virtual Company? Company { get; set; }

        [Display(Name = "Értékesített súly (kg)")]
        public double DistributedKg { get; set; }

        [Display(Name = "Értékesített mennyiség (Liter)")]
        public double DistributedLiter { get; set; }
    }
}