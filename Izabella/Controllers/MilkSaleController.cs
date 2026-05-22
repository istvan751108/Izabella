using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Izabella.Controllers
{
    public class MilkSaleController : Controller
    {
        private readonly IzabellaDbContext _context;

        public MilkSaleController(IzabellaDbContext context)
        {
            _context = context;
        }

        // 1. Index: szűréssel kiegészítve
        public async Task<IActionResult> Index(string monthFilter, DateTime? dayFilter)
        {
            DateTime targetMonth = DateTime.Today;
            if (!string.IsNullOrEmpty(monthFilter) && DateTime.TryParse(monthFilter + "-01", out DateTime parsedDate))
            {
                targetMonth = parsedDate;
            }

            // A választott nap, ha nincs megadva, akkor a mai nap
            DateTime targetDay = dayFilter ?? DateTime.Today;

            // Lekérjük a teljes hónapot a dekád és havi összesítésekhez
            var monthlySales = await _context.MilkSales
                .Where(s => s.DeliveryDate.Month == targetMonth.Month && s.DeliveryDate.Year == targetMonth.Year)
                .OrderByDescending(s => s.DeliveryDate)
                .ThenBy(s => s.DailyDeliverySequence)
                .ToListAsync();

            // Lekérjük a specifikus napot a napi összesítőhöz
            var dailySales = await _context.MilkSales
                .Where(s => s.DeliveryDate == targetDay.Date)
                .ToListAsync();

            ViewBag.CurrentMonthFilter = targetMonth.ToString("yyyy-MM");
            ViewBag.CurrentMonthDisplay = targetMonth;
            ViewBag.CurrentDayFilter = targetDay.ToString("yyyy-MM-dd");

            // Napi statisztikák küldése ViewBag-ben
            ViewBag.DailyWeight = dailySales.Sum(s => s.WeightKg);
            ViewBag.DailyValue = dailySales.Sum(s => s.TotalValue);
            ViewBag.DailyCount = dailySales.Count;

            return View(monthlySales);
        }

        // 2. Create (GET) - Az előző napi ár öröklésével
        public async Task<IActionResult> Create()
        {
            var lastSale = await _context.MilkSales
                .OrderByDescending(s => s.DeliveryDate)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            var model = new MilkSale
            {
                DeliveryDate = DateTime.Today,
                UnitPrice = lastSale?.UnitPrice ?? 0 // Alapértelmezettként az utolsó ár
            };

            return View(model);
        }

        // 3. Create (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MilkSale milkSale)
        {
            if (ModelState.IsValid)
            {
                var existingDeliveriesCount = await _context.MilkSales
                    .Where(s => s.DeliveryDate == milkSale.DeliveryDate)
                    .CountAsync();

                milkSale.DailyDeliverySequence = existingDeliveriesCount + 1;

                _context.Add(milkSale);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"A(z) {milkSale.DailyDeliverySequence}. fuvar sikeresen rögzítve EKÁER számmal.";
                return RedirectToAction(nameof(Index));
            }
            return View(milkSale);
        }

        // 4. Edit (GET) - Betöltjük a módosítandó fuvart
        public async Task<IActionResult> Edit(int id)
        {
            var sale = await _context.MilkSales.FindAsync(id);
            if (sale == null)
            {
                return NotFound();
            }
            return View(sale);
        }

        // 5. Edit (POST) - Utólagos korrekció mentése
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MilkSale milkSale)
        {
            if (id != milkSale.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(milkSale);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"A(z) {milkSale.DeliveryDate.ToString("yyyy.MM.dd.")}-i ({milkSale.DailyDeliverySequence}. sz.) fuvar adatai sikeresen frissítve.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.MilkSales.Any(e => e.Id == milkSale.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(milkSale);
        }

        // 6. CloseEkaer (POST)
        [HttpPost]
        public async Task<IActionResult> CloseEkaer(int id)
        {
            var sale = await _context.MilkSales.FindAsync(id);
            if (sale != null)
            {
                sale.IsEkaerClosed = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"A(z) {sale.EkaerNumber} számú EKÁER lezárva.";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: MilkSale/Distribute?date=2026-05-19
        public async Task<IActionResult> Distribute(DateTime date)
        {
            var companies = await _context.Companies.OrderBy(c => c.Name).ToListAsync();
            var existingDistributions = await _context.MilkCompanyDistributions
                .Where(d => d.DistributionDate == date.Date)
                .ToListAsync();

            var totalDaySales = await _context.MilkSales
                .Where(s => s.DeliveryDate == date.Date)
                .SumAsync(s => s.WeightKg);

            ViewBag.TargetDate = date;
            ViewBag.TotalDayKg = totalDaySales;
            ViewBag.TotalDayLiter = totalDaySales * 0.971;

            // Előkészítjük a nézetnek az adatokat
            var viewModel = companies.Select(c => new MilkDistributionVM
            {
                CompanyId = c.Id,
                CompanyName = c.Name,
                Kg = existingDistributions.FirstOrDefault(d => d.CompanyId == c.Id)?.DistributedKg ?? 0,
                Liter = existingDistributions.FirstOrDefault(d => d.CompanyId == c.Id)?.DistributedLiter ?? 0
            }).ToList();

            return View(viewModel);
        }

        // POST: MilkSale/Distribute
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Distribute(DateTime date, List<MilkDistributionVM> model)
        {
            // Régi leosztások törlése az adott napra (felülírás miatt)
            var oldDistributions = _context.MilkCompanyDistributions.Where(d => d.DistributionDate == date.Date);
            _context.MilkCompanyDistributions.RemoveRange(oldDistributions);

            foreach (var item in model)
            {
                if (item.Kg > 0 || item.Liter > 0)
                {
                    _context.MilkCompanyDistributions.Add(new MilkCompanyDistribution
                    {
                        DistributionDate = date.Date,
                        CompanyId = item.CompanyId,
                        DistributedKg = item.Kg,
                        DistributedLiter = item.Liter
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"A(z) {date.ToString("yyyy.MM.dd.")}-i napi tejmennyiség céges felosztása mentve.";
            return RedirectToAction("Index");
        }
    }
}