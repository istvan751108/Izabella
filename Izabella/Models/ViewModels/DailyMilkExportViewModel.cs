using System;
using System.ComponentModel.DataAnnotations;

namespace Izabella.Models.ViewModels
{
    public class DailyMilkExportViewModel
    {
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Kiválasztott nap")]
        public DateTime TargetDate { get; set; }
    }
}