using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public enum SemenProductionMethod { Természetes, Mesterséges }
    public enum SemenType { Fagyasztott, Friss }
    public enum SemenOrigin { Hazai, Import }

    public class BullSemen
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "KLSZ")]
        public string Klsz { get; set; }

        [Required]
        [Display(Name = "Bika neve")]
        public string BullName { get; set; }

        [Required]
        [Display(Name = "Termelési szám / Lefejtés")]
        public string ProductionNumber { get; set; }

        [Required]
        [Display(Name = "Fajta")]
        public string Breed { get; set; } = "Holstein-fríz";

        [Display(Name = "Szállító neve")]
        public string? SupplierName { get; set; }

        [Display(Name = "Beszerzési ár (Ft/adag)")]
        [DisplayFormat(DataFormatString = "{0:F0}", ApplyFormatInEditMode = true)] // F0 = nulla tizedesjegy
        public decimal? PurchasePrice { get; set; }

        [Required]
        [Display(Name = "Aktuális készlet (adag)")]
        public int StockQuantity { get; set; }

        [Display(Name = "Tároló konténer / Kaniszter")]
        public string? ContainerId { get; set; }

        [Display(Name = "Termelés módja")]
        public SemenProductionMethod ProductionMethod { get; set; } = SemenProductionMethod.Mesterséges;

        [Display(Name = "Sperma típusa")]
        public SemenType Type { get; set; } = SemenType.Fagyasztott;

        [Display(Name = "Származás")]
        public SemenOrigin Origin { get; set; } = SemenOrigin.Import;

        [Display(Name = "Szexált sperma?")]
        public bool IsSexed { get; set; } = true;

        [Display(Name = "Ciklusbika?")]
        public bool IsCycleBull { get; set; } = false;

        [Display(Name = "Első felhasználás")]
        public DateTime? FirstUseDate { get; set; }

        [Display(Name = "Utolsó felhasználás")]
        public DateTime? LastUseDate { get; set; }

        [Display(Name = "Aktív?")]
        public bool IsActive { get; set; } = true;
    }
}