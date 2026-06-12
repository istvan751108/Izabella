using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;

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
    public DbSet<SemenTransaction> SemenTransactions { get; set; }
    public DbSet<SupportFormConfig> SupportFormConfigs { get; set; }
    public DbSet<DailyStat> DailyStats { get; set; }

    public DbSet<MilkDataStaging> MilkDataStagings { get; set; }

    public DbSet<MilkProduction> MilkProductions { get; set; }
    public DbSet<DryOffEvent> DryOffEvents { get; set; }
    public DbSet<Medication> Medications { get; set; }
    public DbSet<MilkSale> MilkSales { get; set; }
    public DbSet<MilkQualityLab> MilkQualityLabs { get; set; }
    public DbSet<MilkCompanyDistribution> MilkCompanyDistributions { get; set; }
    public DbSet<MilkLabResult> MilkLabResults { get; set; }
    public DbSet<AnimalTreatment> AnimalTreatments { get; set; }
    public DbSet<HeiferSalesBuffer> HeiferSalesBuffers { get; set; }

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