using System;
using System.Collections.Generic;

namespace Izabella.Models;

public partial class Voucher
{
    public int Id { get; set; }

    public int Year { get; set; }

    public int Warehouse { get; set; }

    public int SequenceNumber { get; set; }

    public string Type { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
