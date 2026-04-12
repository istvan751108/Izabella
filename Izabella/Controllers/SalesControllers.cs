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
    DateTime saleDate, string receiptNumber, decimal[] grossWeights, decimal[] unitPrices, string[] deductions) // string[]-ként fogadjuk a biztonság kedvéért
        {
            if (cattleIds == null) return RedirectToAction("Index", "Cattles");

            for (int i = 0; i < cattleIds.Length; i++)
            {
                var cattle = await _context.Cattles.FindAsync(cattleIds[i]);
                if (cattle == null) continue;

                // --- ÚJ LOGIKA A LEVONÁS MEGHATÁROZÁSÁHOZ ---
                double finalDeduction = 8.0; // Alapértelmezett, ha minden kötél szakad

                // 1. Megpróbáljuk kiolvasni a tömbből, amit a felhasználó beírt
                if (deductions != null && deductions.Length > i)
                {
                    // Megpróbáljuk értelmezni a számot (ponttal és vesszővel is)
                    string rawValue = deductions[i].Replace(",", ".");
                    double.TryParse(rawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out finalDeduction);
                }

                // 2. Ha a kapott érték 0 (vagy hibás volt a beküldés), 
                // akkor az ADATBÁZISBÓL nézzük meg a korcsoportot és kényszerítsük a szabályt
                if (finalDeduction == 0)
                {
                    string group = cattle.AgeGroup ?? "";
                    if (group.Contains("Borjú", StringComparison.OrdinalIgnoreCase)) finalDeduction = 6.0;
                    else if (group.Contains("Tehén", StringComparison.OrdinalIgnoreCase)) finalDeduction = 8.5;
                    else finalDeduction = 8.0;
                }

                // --- SZÁMÍTÁS ---
                decimal gross = grossWeights[i];
                decimal price = unitPrices[i];
                decimal net = gross * (1m - ((decimal)finalDeduction / 100m));

                net = Math.Round(net, 2);
                decimal total = Math.Round(net * price, 2);

                var transaction = new SaleTransaction
                {
                    CattleId = cattle.Id,
                    CustomerId = customerId,
                    SaleDate = saleDate,
                    Type = saleType,
                    ReceiptNumber = receiptNumber,
                    GrossWeight = gross,
                    DeductionPercentage = finalDeduction, // Ezt mentjük el
                    NetWeight = net,
                    UnitPrice = price,
                    TotalNetPrice = total,
                    IsReported = false
                };

                // Állat kivezetése
                cattle.IsActive = false;
                cattle.ExitDate = saleDate;
                cattle.ExitType = (ExitType)saleType;
                if (saleType == SaleType.Slaughter) cattle.IsAlive = false;

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
        public async Task<IActionResult> GenerateEnar5136Xml()
        {
            // Minden olyan tranzakció, ami még nincs lejelentve
            var pendingItems = await _context.SaleTransactions
                .Include(s => s.Cattle)
                .Include(s => s.Customer)
                .Where(t => !t.IsReported)
                .ToListAsync();

            if (!pendingItems.Any())
            {
                TempData["Info"] = "Nincs bejelentésre váró esemény.";
                return RedirectToAction("Index");
            }

            // XML struktúra építése (az 5136 bizonylat sémája alapján)
            var doc = new System.Xml.Linq.XDocument(
                new System.Xml.Linq.XElement("EnarAdatcsere",
                    new System.Xml.Linq.XElement("Fejlec",
                        new System.Xml.Linq.XElement("BizonylatTipus", "5136"),
                        new System.Xml.Linq.XElement("KuldőTenyészet", "1234567") // Ide írd a saját kódotokat
                    ),
                    new System.Xml.Linq.XElement("Mozgasok",
                        pendingItems.Select(item => new System.Xml.Linq.XElement("Mozgas",
                            new System.Xml.Linq.XElement("Fulszam", item.Cattle.EarTag),
                            new System.Xml.Linq.XElement("Datum", item.SaleDate.ToString("yyyy-MM-dd")),
                            new System.Xml.Linq.XElement("MozgasKod", item.UnitPrice == 0 ? "511" : "211"), // 511: Elhullás, 211: Eladás
                            new System.Xml.Linq.XElement("Bizonylatszam", item.ReceiptNumber)
                        ))
                    )
                )
            );

            // Miután legeneráltuk, megjelöljük őket, hogy ne kerüljenek bele újra
            foreach (var item in pendingItems)
            {
                item.IsReported = true;
            }
            await _context.SaveChangesAsync();

            var content = System.Text.Encoding.UTF8.GetBytes(doc.ToString());
            return File(content, "application/xml", $"ENAR_5136_{DateTime.Now:yyyyMMdd_HHmm}.xml");
        }
        public async Task<IActionResult> MonthlyReport(int? year, int? month)
        {
            var y = year ?? DateTime.Now.Year;
            var m = month ?? DateTime.Now.Month;

            // 1. Értékesítések és Elhullások (mint eddig)
            var transactions = await _context.SaleTransactions
                .Include(s => s.Customer)
                .Include(s => s.Cattle).ThenInclude(c => c.CurrentHerd)
                .Where(s => s.SaleDate.Year == y && s.SaleDate.Month == m)
                .ToListAsync();

            // 2. Ellések (az adott hónapban született borjak)
            // Beemeljük az anya adatait is a korcsoport miatt
            var newborns = await _context.Cattles
                .Where(c => c.BirthDate.Year == y && c.BirthDate.Month == m && c.MotherEnar != null)
                .ToListAsync();

            // Anya adatok lekérése a korcsoport statisztikához
            var motherEnars = newborns.Select(n => n.MotherEnar).Distinct().ToList();
            var mothers = await _context.Cattles
                .Where(c => motherEnars.Contains(c.EnarNumber))
                .ToListAsync();

            ViewBag.Year = y;
            ViewBag.Month = m;
            ViewBag.Newborns = newborns;
            ViewBag.Mothers = mothers;

            return View(transactions);
        }
    }
}