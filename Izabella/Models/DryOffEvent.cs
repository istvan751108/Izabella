namespace Izabella.Models
{
    public class DryOffEvent
    {
        public int Id { get; set; }
        public int CattleId { get; set; }
        public Cattle Cattle { get; set; }
        public DateTime DryOffDate { get; set; }    // A kezelés napja
        public int MedicationId { get; set; }
        public Medication Medication { get; set; }
        public bool IsSeparated { get; set; }        // Elkerült-e már a fejősök közül?
        public DateTime? SeparationDate { get; set; }
        public string Note { get; set; }             // Megjegyzés (pl. "nem állt le azonnal")
    }
}