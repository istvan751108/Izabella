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
    public DbSet<Cattle> Cattles { get; set; }
    public DbSet<BreedingData> BreedingDatas { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Herd> Herds { get; set; }
    public DbSet<DeathLog> DeathLogs { get; set; }
    public DbSet<DeathReason> DeathReasons { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<SaleTransaction> SaleTransactions { get; set; }
    public DbSet<AnimalHistory> AnimalHistories { get; set; }
    public DbSet<WeightBuffer> WeightBuffers { get; set; }
    public DbSet<BullSemen> BullSemens { get; set; }
    public DbSet<Staff> Staffs { get; set; }
    public DbSet<MatingSuggestion> MatingSuggestions { get; set; }
    public DbSet<InseminationLog> InseminationLogs { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Állat -> Tenyészet kapcsolat (Törléskor ne nyúljon az állathoz)
        modelBuilder.Entity<Cattle>()
            .HasOne(c => c.CurrentHerd)
            .WithMany()
            .HasForeignKey(c => c.CurrentHerdId)
            .OnDelete(DeleteBehavior.Restrict);

        // 2. Tenyészet -> Cég kapcsolat (Biztonsági tartalék)
        modelBuilder.Entity<Herd>()
            .HasOne(h => h.Company)
            .WithMany()
            .HasForeignKey(h => h.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
