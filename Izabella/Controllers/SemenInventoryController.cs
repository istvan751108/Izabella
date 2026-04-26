using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Controllers
{
    public class SemenInventoryController : Controller
    {
        private readonly IzabellaDbContext _context;
        public SemenInventoryController(IzabellaDbContext context) { _context = context; }

        public async Task<IActionResult> Index()
        {
            var stock = await _context.BullSemens.Where(s => s.IsActive).ToListAsync();
            return View(stock);
        }

        [HttpGet]
        public IActionResult Create() => View(new BullSemen());

        public async Task<IActionResult> Details(int id)
        {
            var semen = await _context.BullSemens.FindAsync(id);
            if (semen == null) return NotFound();
            return View(semen);
        }

        [HttpPost]
        public async Task<IActionResult> Create(BullSemen semen)
        {
            if (ModelState.IsValid)
            {
                _context.Add(semen);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Bikasperma sikeresen rögzítve.";
                return RedirectToAction(nameof(Index));
            }
            // Hiba esetén ne redirect legyen, hanem maradjon a View-n a hibaüzenetekkel!
            return View(semen);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var semen = await _context.BullSemens.FindAsync(id);
            if (semen == null) return NotFound();
            return View(semen);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(BullSemen semen)
        {
            if (ModelState.IsValid)
            {
                _context.Update(semen);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Adatok frissítve.";
                return RedirectToAction(nameof(Index));
            }
            return View(semen);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var semen = await _context.BullSemens.FindAsync(id);
            if (semen == null) return NotFound();

            // ELLENŐRZÉS: Ha már volt használva (FirstUseDate nem null), nem engedjük törölni
            if (semen.FirstUseDate.HasValue)
            {
                TempData["Error"] = "Ez a tétel nem törölhető, mert már fel lett használva termékenyítéshez! Csak inaktiválni lehet.";
                return RedirectToAction(nameof(Index));
            }

            // Ha még szűz a tétel, akkor törölhető
            _context.BullSemens.Remove(semen);
            await _context.SaveChangesAsync();

            TempData["Success"] = "A tétel véglegesen törölve lett.";
            return RedirectToAction(nameof(Index));
        }
        // GET: Bevételezés / Új tétel felvétele meglévő alapján
        [HttpGet]
        public async Task<IActionResult> Restock(int? sourceId)
        {
            if (sourceId.HasValue)
            {
                var source = await _context.BullSemens.FindAsync(sourceId.Value);
                if (source != null)
                {
                    var newSemen = new BullSemen
                    {
                        Klsz = source.Klsz,
                        BullName = source.BullName,
                        ProductionNumber = source.ProductionNumber, // Alapból másoljuk az előzőt!
                        Breed = source.Breed,
                        SupplierName = source.SupplierName,
                        PurchasePrice = source.PurchasePrice,
                        ContainerId = source.ContainerId,
                        ProductionMethod = source.ProductionMethod,
                        Type = source.Type,
                        Origin = source.Origin,
                        IsSexed = source.IsSexed,
                        IsCycleBull = source.IsCycleBull,
                        StockQuantity = 0
                    };
                    return View("Create", newSemen);
                }
            }
            return View("Create", new BullSemen());
        }

        [HttpPost]
        public async Task<IActionResult> ProcessRestock(BullSemen input)
        {
            // Ha a modell nem érvényes (pl. üres a ProductionNumber), küldjük vissza a View-ra
            if (!ModelState.IsValid)
            {
                return View("Create", input);
            }

            var existing = await _context.BullSemens
                .FirstOrDefaultAsync(s => s.Klsz == input.Klsz &&
                                         s.ProductionNumber == input.ProductionNumber &&
                                         s.IsActive);

            if (existing != null)
            {
                existing.StockQuantity += input.StockQuantity;
                if (input.PurchasePrice.HasValue) existing.PurchasePrice = input.PurchasePrice;
                existing.SupplierName = input.SupplierName;
                _context.Update(existing);
                TempData["Success"] = $"Készlet frissítve: {existing.BullName} (+{input.StockQuantity} adag)";
            }
            else
            {
                // Új tétel rögzítése (ebben az esetben az Id-t 0-ra állítjuk, hogy az EF újként kezelje)
                input.Id = 0;
                _context.Add(input);
                TempData["Success"] = $"Új tétel rögzítve: {input.BullName} ({input.ProductionNumber})";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> ScrapSemen(int id, int amount, string reason)
        {
            var semen = await _context.BullSemens.FindAsync(id);
            if (semen != null && amount > 0 && semen.StockQuantity >= amount)
            {
                semen.StockQuantity -= amount;
                // Opcionálisan naplózhatjuk is egy új táblába a selejtezést
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{amount} adag selejtezve ({semen.BullName}).";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
