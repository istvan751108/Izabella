using System;
using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class LiquidManure
    {
        [Key]
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public double TotalAmount { get; set; }

        public double Cow { get; set; }
        public double Young6_9 { get; set; }
        public double Young9_12 { get; set; }
        public double Young12Preg { get; set; }
        public double PregnantHeifer { get; set; }

        public string VoucherIn { get; set; } = string.Empty;
        public string VoucherOut { get; set; } = string.Empty;
        public List<LiquidManureSplit> Splits { get; set; } = new();
    }
}
