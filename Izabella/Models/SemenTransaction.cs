namespace Izabella.Models
{
    public enum TransactionType
    {
        Insemination = 1,    // Termékenyítés
        ReInsemination = 2,  // Rátermékenyítés
        Scrap = 3,           // Selejtezés
        StockIn = 4          // Készlet bevételezés (opcionális)
    }

    public class SemenTransaction
    {
        public int Id { get; set; }
        public int BullSemenId { get; set; }
        public BullSemen BullSemen { get; set; }

        public DateTime Date { get; set; }
        public int Amount { get; set; } // Hány adag (általában 1)
        public TransactionType Type { get; set; }

        public string? CattleEarTag { get; set; } // Ha termékenyítés, melyik állat
        public string? Comment { get; set; }      // Pl. "Elpattant ampulla"
        public string? PerformedBy { get; set; }  // Ki rögzítette
    }
}
