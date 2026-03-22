using System;
using System.Collections.Generic;

namespace Izabella.Models;

public class SolidManureLoad
{
    public int Id { get; set; }
    public DateTime Date { get; set; }

    public string LicensePlate { get; set; }

    public double GrossWeight { get; set; }
    public double TareWeight { get; set; }
    public double NetWeight { get; set; }

    public string Destination { get; set; }
}
