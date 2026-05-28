using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Izabella.Models
{
    public class MilkLabResult
    {
        [Key]
        public int Id { get; set; }

        // Összekötés az állattal
        [Required]
        public int CattleId { get; set; }

        [ForeignKey("CattleId")]
        public virtual Cattle Cattle { get; set; }

        // XML Fejléc adatok az ellenőrzéshez
        [Required]
        [StringLength(2)]
        public string Megye { get; set; }

        [Required]
        [StringLength(3)]
        public string Tenyeszet { get; set; }

        [Required]
        [StringLength(2)]
        public string Telep { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime BefDat { get; set; } // Befejezés/mintavétel napja

        [DataType(DataType.Date)]
        public DateTime? BefDatTol { get; set; }

        [DataType(DataType.Date)]
        public DateTime ModDat { get; set; } // Labor módosítási dátuma

        [StringLength(1)]
        public string AllKod { get; set; }

        // Összesített napi mutatók
        public double NapiTej { get; set; }

        public double NapiZsir { get; set; }
        public double NapiFeherje { get; set; }
        public int SzomatikusSejtszam { get; set; }
        public double Karbamid { get; set; }

        [StringLength(10)]
        public string? EllMod { get; set; }

        [StringLength(5)]
        public string? AzTipus { get; set; }

        // RÉSZLETES MINTAVÉTELI ADATOK (1 - 6 fejési műszak/napszak)
        public double Tej1 { get; set; }

        public double Zsir1 { get; set; }
        public double Feherje1 { get; set; }
        public double Cukor1 { get; set; }

        [StringLength(10)]
        public string? Vonalkod1 { get; set; }

        public DateTime? Idopont1 { get; set; }

        public double Tej2 { get; set; }
        public double Zsir2 { get; set; }
        public double Feherje2 { get; set; }
        public double Cukor2 { get; set; }

        [StringLength(10)]
        public string? Vonalkod2 { get; set; }

        public DateTime? Idopont2 { get; set; }

        public double Tej3 { get; set; }
        public double Zsir3 { get; set; }
        public double Feherje3 { get; set; }
        public double Cukor3 { get; set; }

        [StringLength(10)]
        public string? Vonalkod3 { get; set; }

        public DateTime? Idopont3 { get; set; }

        public double Tej4 { get; set; }
        public double Zsir4 { get; set; }
        public double Feherje4 { get; set; }
        public double Cukor4 { get; set; }

        [StringLength(10)]
        public string? Vonalkod4 { get; set; }

        public DateTime? Idopont4 { get; set; }

        public double Tej5 { get; set; }
        public double Zsir5 { get; set; }
        public double Feherje5 { get; set; }
        public double Cukor5 { get; set; }

        [StringLength(10)]
        public string? Vonalkod5 { get; set; }

        public DateTime? Idopont5 { get; set; }

        public double Tej6 { get; set; }
        public double Zsir6 { get; set; }
        public double Feherje6 { get; set; }
        public double Cukor6 { get; set; }

        [StringLength(10)]
        public string? Vonalkod6 { get; set; }

        public DateTime? Idopont6 { get; set; }

        // Importálási napló
        public DateTime ImportedAt { get; set; } = DateTime.Now;
    }
}