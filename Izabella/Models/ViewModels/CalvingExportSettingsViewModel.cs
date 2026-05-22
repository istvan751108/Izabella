using System.ComponentModel.DataAnnotations;

namespace Izabella.Models.ViewModels
{
    public class CalvingExportSettingsViewModel
    {
        public string Megye { get; set; } = "14";
        public string Tenyeszet { get; set; } = "341";
        public string Telep { get; set; } = "21";

        public DateTime BefejesDatuma { get; set; } = DateTime.Now;
        public DateTime UtolsoBefejesDatuma { get; set; } // Ezt az adatbázisból vagy beállításokból olvassuk ki előző futás alapján

        [Required]
        [Display(Name = "Országos ENAR Tenyészetkód")]
        public string EnarTeny { get; set; } = "467355"; // Alapértelmezett érték
    }
}