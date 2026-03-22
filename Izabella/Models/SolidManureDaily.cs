using System;
using System.Collections.Generic;

namespace Izabella.Models;

public class SolidManureDaily
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public double TotalNet { get; set; }

    public double Cow { get; set; }
    public double CalfMilk { get; set; }
    public double Calf3_6 { get; set; }
    public double Young6_9 { get; set; }
    public double Young9_12 { get; set; }
    public double PregnantHeifer { get; set; }
}
