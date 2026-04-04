namespace Izabella.Models
{
    public class DeathLog
    {
        public int Id { get; set; }
        public int CattleId { get; set; }
        public virtual Cattle? Cattle { get; set; }

        public DateTime DeathDate { get; set; }
        public string Reason { get; set; } // Itt tároljuk majd a választott okot
        public double EstimatedWeight { get; set; }
        public string EarTagAtDeath { get; set; }
        public string EnarNumberAtDeath { get; set; }
        public bool IsEnarReported { get; set; } = false;
    }
}