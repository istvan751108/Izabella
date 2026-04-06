using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Izabella.Models
    {
    public class SaleTransaction
    {
        public int Id { get; set; }
        public DateTime SaleDate { get; set; }
        public SaleType Type { get; set; }

        public int CustomerId { get; set; }
        public virtual Customer Customer { get; set; }
        public int CattleId { get; set; }
        public virtual Cattle Cattle { get; set; }

        public string? ReceiptNumber { get; set; } // Itt a kérdőjel fontos!

        [Column(TypeName = "decimal(18, 2)")]
        public decimal GrossWeight { get; set; }

        public double DeductionPercentage { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal NetWeight { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalNetPrice { get; set; }
        public bool IsReported { get; set; } = false;
    }
}
