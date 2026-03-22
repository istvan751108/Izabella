using Izabella.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
namespace Izabella.Services
{
    public class VoucherService
    {
        private readonly IzabellaDbContext _context;

        public VoucherService(IzabellaDbContext context)
        {
            _context = context;
        }

        public async Task<Voucher> CreateVoucherAsync(string type)
        {
            var year = DateTime.Now.Year;
            var warehouse = 204;

            int maxRetry = 5;

            for (int attempt = 0; attempt < maxRetry; attempt++)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var lastNumber = await _context.Vouchers
                        .Where(v => v.Year == year && v.Warehouse == warehouse && v.Type == type)
                        .OrderByDescending(v => v.SequenceNumber)
                        .Select(v => v.SequenceNumber)
                        .FirstOrDefaultAsync();

                    var nextNumber = lastNumber + 1;

                    var voucher = new Voucher
                    {
                        Year = year,
                        Warehouse = warehouse,
                        SequenceNumber = nextNumber,
                        Type = type,
                        CreatedAt = DateTime.Now
                    };

                    _context.Vouchers.Add(voucher);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return voucher;
                }
                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync();

                    if (ex.InnerException?.Message.Contains("UQ_Voucher") == true)
                    {
                        continue;
                    }

                    throw;
                }
            }

            throw new Exception("Nem sikerült bizonylatot generálni.");
        }

        public string FormatVoucher(Voucher v)
        {
            return $"{v.Type} {v.Year}/{v.Warehouse}/{v.SequenceNumber:D6}";
        }
    }
}