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
    }
}