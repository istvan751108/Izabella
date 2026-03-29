using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Models;

public class IzabellaDbContext : IdentityDbContext
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
    public DbSet<LiquidManureSplit> LiquidManureSplits { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // EZ A SOR A KULCS: Ez állítja be az Identity táblák (köztük a Passkey) kulcsait!
        base.OnModelCreating(modelBuilder);

        // Itt tarthatod a saját egyedi beállításaidat, ha vannak, például:
        // modelBuilder.Entity<LiquidManure>().HasMany(x => x.Splits)...
    }
}
