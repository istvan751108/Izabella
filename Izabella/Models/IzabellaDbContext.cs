using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Models;

using Microsoft.EntityFrameworkCore;

public class IzabellaDbContext : DbContext
{
    public IzabellaDbContext(DbContextOptions<IzabellaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<SolidManureLoad> SolidManureLoads { get; set; }
    public DbSet<SolidManureDaily> SolidManureDailies { get; set; }
    public DbSet<LiquidManure> LiquidManures { get; set; }
    public DbSet<SolidManure> SolidManures { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
     
    }
}
