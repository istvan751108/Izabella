namespace Izabella.Models
{
    public class Medication
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }     // ml, tubus, g
        public double DefaultDose { get; set; }
        public bool IsAntibiotic { get; set; }
        public TreatmentCategory Category { get; set; }

        public int WithdrawalPeriodMilk { get; set; } // ÉVI tej (nap)

        public int WithdrawalPeriodMeat { get; set; } // ÉVI hús (nap)
    }
}