using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Izabella.Models;

namespace Izabella.Controllers
{
    public partial class SalesController : Controller
    {
        private readonly IzabellaDbContext _context;

        public SalesController(IzabellaDbContext context)
        {
            _context = context;
        }

        // GET: Sales/Index - Az értékesítési napló
        public async Task<IActionResult> Index()
        {
            var sales = await _context.SaleTransactions
                .Include(s => s.Customer)
                .Include(s => s.Cattle)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
            return View(sales);
        }

        // POST: Sales/Checkout - A kijelölt állatok fogadása az Indexről
        [HttpPost]
        public async Task<IActionResult> Checkout(int[] selectedCattleIds)
        {
            if (selectedCattleIds == null || selectedCattleIds.Length == 0)
            {
                TempData["Error"] = "Nincs kijelölve állat az értékesítéshez!";
                return RedirectToAction("Index", "Cattles");
            }

            var selectedCattle = await _context.Cattles
                .Where(c => selectedCattleIds.Contains(c.Id))
                .ToListAsync();

            ViewBag.CustomerId = new SelectList(_context.Customers, "Id", "Name");

            return View(selectedCattle);
        }

        // POST: Sales/ConfirmSale - A véglegesítés és mentés (Már a súlyokkal és bizonylatszámmal)
        [HttpPost]
        public async Task<IActionResult> ConfirmSale(int[] cattleIds, int customerId, SaleType saleType,
    DateTime saleDate, string receiptNumber, decimal[] grossWeights, decimal[] unitPrices, double[] deductions)
        {
            // Ellenőrizzük, hogy megkaptuk-e a tömböket
            if (cattleIds == null || grossWeights == null || unitPrices == null)
                return RedirectToAction("Index", "Cattles");
            for (int i = 0; i < cattleIds.Length; i++)
            {
                var cattle = await _context.Cattles.FindAsync(cattleIds[i]);
                if (cattle == null) continue;

                decimal gross = grossWeights[i];
                decimal price = unitPrices[i];

                // Kicsit finomított védelem:
                double deduction = 8.0; // Alapértelmezett
                if (deductions != null && deductions.Length > i)
                {
                    deduction = deductions[i];
                }
                // Ha valamiért 0-át kapnánk (mert nem sikerült a parsolás), de az állat borjú, 
                // adjunk neki még egy esélyt a helyes értékre, de csak ha deductions[i] tényleg 0 volt
                if (deduction == 0)
                {
                    deduction = cattle.AgeGroup.Contains("Borjú") ? 6.0 : (cattle.AgeGroup == "Tehén" ? 8.5 : 8.0);
                }

                // Számítás
                decimal net = gross * (1 - ((decimal)deduction / 100m));
                decimal total = net * price;

                var transaction = new SaleTransaction
                {
                    CattleId = cattle.Id,
                    CustomerId = customerId,
                    SaleDate = saleDate,
                    Type = saleType,
                    ReceiptNumber = receiptNumber ?? "",
                    GrossWeight = gross,
                    DeductionPercentage = deduction,
                    NetWeight = net,
                    UnitPrice = price,
                    TotalNetPrice = total,
                    IsReported = false // ÚJ MEZŐ: Jelezzük, hogy még nincs XML-be foglalva
                };

                cattle.IsActive = false;
                cattle.ExitDate = saleDate;
                cattle.ExitType = (ExitType)saleType;

                _context.SaleTransactions.Add(transaction);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
        public async Task<IActionResult> Edit(int id)
        {
            var transaction = await _context.SaleTransactions
                .Include(s => s.Cattle)
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            ViewBag.CustomerId = new SelectList(_context.Customers, "Id", "Name", transaction.CustomerId);
            return View(transaction);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(SaleTransaction model)
        {
            // Itt újra le kell futtatni a nettó súly és végösszeg számítást a módosított bruttó/ár alapján
            model.NetWeight = model.GrossWeight * (decimal)(1 - (model.DeductionPercentage / 100));
            model.TotalNetPrice = model.NetWeight * model.UnitPrice;

            _context.Update(model);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
    }
}