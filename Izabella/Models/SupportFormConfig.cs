using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class SupportFormConfig
    {
        [Key]
        public int Id { get; set; }

        // Engedélyes adatai (3. mező)
        public string LicenseeName { get; set; } = "Alpha-Vet Kft.";
        public string LicenseeClientId { get; set; } = "1003205103";

        // Alapértelmezett gyógyszerek a 2. oldalon (JSON-ként tárolva vagy külön táblában)
        public string Medication1Name { get; set; } = "OVARELIN";
        public string Medication1Agent { get; set; } = "GONADORELIN";

        public string Medication2Name { get; set; } = "SYNCROPROST";
        public string Medication2Agent { get; set; } = "KLOPROSZTENOL";

        public string Medication3Name { get; set; } = "BIOBOS RCC";
        public string Medication3Agent { get; set; } = "ROTA, KORONA, COLI";

        public string FilingPlace { get; set; } = "Nyírbátor";
        // ÚJ MEZŐK A 2335-ÖS (VEMHES) SABLONHOZ
        public string? HeiferMed1Name { get; set; } = "DALMARELIN";
        public string? HeiferMed1Agent { get; set; } = "LECIRELIN";

        public string? HeiferMed2Name { get; set; } = "VETEGLAN";
        public string? HeiferMed2Agent { get; set; } = "KLOPROSZTENOL";

        public string? HeiferMed3Name { get; set; } = "BIOBOS RCC";
        public string? HeiferMed3Agent { get; set; } = "ROTA, KORONA, COLI";
    }
}