using System;
using System.ComponentModel.DataAnnotations;

namespace Izabella.Models
{
    public class SolidManure
    {
        [Key]
        public int Id { get; set; }

        public DateTime Date { get; set; }
        public double Gross { get; set; }
        public double Tare { get; set; }
        public double Net { get; set; }

        public double Cow { get; set; }
        public double CalfMilk { get; set; }
        public double Calf3_6 { get; set; }
        public double Young6_9 { get; set; }
        public double Young9_12 { get; set; }
        public double PregnantHeifer { get; set; }

        public string VoucherIn { get; set; }
        public string VoucherOut { get; set; }
    }
}
