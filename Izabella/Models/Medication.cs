namespace Izabella.Models
{
    public enum TreatmentCategory
    { Tőgy, Láb, Szaporodás, Apasztás, Egyéb }

    public class Medication
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Quantity { get; set; } // Készletmennyiség (pl. ml vagy tubus)
        public string Unit { get; set; }     // ml, tubus, g
        public double DefaultDose { get; set; } // Szokásos adag (pl. 4 tubus/tőgy)
        public bool IsAntibiotic { get; set; }
        public TreatmentCategory Category { get; set; } // Enum: Tőgy, Láb, Szaporodás, Apasztás, Egyéb
        public int WithdrawalPeriodMilk { get; set; } // Élelmezés-egészségügyi várakozási idő (nap)
    }
}