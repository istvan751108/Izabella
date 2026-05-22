using System;
using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class MilkSale
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "A szállítási dátum megadása kötelező.")]
        [DataType(DataType.Date)]
        [Display(Name = "Szállítás dátuma")]
        public DateTime DeliveryDate { get; set; }

        [Required(ErrorMessage = "A teherautó rendszám megadása kötelező.")]
        [Display(Name = "Teherautó rendszám")]
        public string TruckPlateNumber { get; set; }

        [Display(Name = "Pótkocsi rendszám")]
        public string? TrailerPlateNumber { get; set; } // Módosítva: opcionális (nullable)

        [Required(ErrorMessage = "A súly megadása kötelező.")]
        [Range(1, 100000, ErrorMessage = "A súlynak 1 és 100 000 kg között kell lennie.")]
        [Display(Name = "Súly (kg)")]
        public double WeightKg { get; set; }

        [Required(ErrorMessage = "Az egységár megadása kötelező.")]
        [Display(Name = "Egységár (Ft/kg)")]
        public double UnitPrice { get; set; }

        [Display(Name = "Szállítmány értéke (Ft)")]
        public double TotalValue => WeightKg * UnitPrice;

        [Required(ErrorMessage = "Az EKÁER szám megadása kötelező.")]
        [StringLength(15)]
        [Display(Name = "EKÁER szám")]
        public string EkaerNumber { get; set; }

        public bool IsEkaerClosed { get; set; }
        public int DailyDeliverySequence { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}