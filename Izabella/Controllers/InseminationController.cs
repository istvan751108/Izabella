using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Controllers
{
    public class InseminationController : Controller
    {
        private readonly IzabellaDbContext _context;
        public InseminationController(IzabellaDbContext context) { _context = context; }

        public async Task<IActionResult> Index(string searchEarTag)
        {
            if (string.IsNullOrEmpty(searchEarTag)) return View(new InseminationLog());

            var cattle = await _context.Cattles.FirstOrDefaultAsync(c => c.EarTag.Trim() == searchEarTag.Trim());

            if (cattle == null)
            {
                TempData["Error"] = "Az állat nem található!";
                return View(new InseminationLog());
            }

            // Ha megvan az állat, küldjük át a Create oldalra
            return RedirectToAction(nameof(Create), new { earTag = cattle.EarTag.Trim() });
        }

        public async Task<IActionResult> Create(string earTag)
        {
            var cattle = await _context.Cattles.FirstOrDefaultAsync(c => c.EarTag == earTag);
            if (cattle == null) return RedirectToAction(nameof(Index));

            // Kor ellenőrzés
            if (cattle.BirthDate > DateTime.Now.AddMonths(-12))
            {
                TempData["Error"] = "Az állat még nincs 12 hónapos!";
                return RedirectToAction(nameof(Index));
            }

            // Utolsó termékenyítés lekérése a rátermékenyítés szabályhoz (max 48 óra / másnap végéig)
            var lastInsem = await _context.InseminationLogs
                .Where(l => l.CattleEarTag == earTag)
                .OrderByDescending(l => l.EventDate)
                .FirstOrDefaultAsync();

            ViewBag.LastInsem = lastInsem;
            ViewBag.CanReInseminate = false;

            if (lastInsem != null)
            {
                // Szabály: Mai vagy tegnapi termékenyítés fogadható el rátermékenyítésnek
                if (lastInsem.EventDate.Date >= DateTime.Now.Date.AddDays(-1))
                {
                    ViewBag.CanReInseminate = true;
                }
            }

            ViewBag.Suggestions = await _context.MatingSuggestions.Where(s => s.CattleEarTag == earTag).OrderBy(s => s.Priority).ToListAsync();
            ViewBag.Inseminators = await _context.Staffs.Where(s => s.IsActive && s.Role == StaffRole.Inszeminátor).ToListAsync();
            ViewBag.Markers = await _context.Staffs.Where(s => s.IsActive && s.Role == StaffRole.Jelölő).ToListAsync();
            ViewBag.Inventory = await _context.BullSemens.Where(s => s.IsActive && s.StockQuantity > 0).ToListAsync();

            return View(new InseminationLog { CattleEarTag = earTag, EventDate = DateTime.Now });
        }

        [HttpPost]
        public async Task<IActionResult> Save(InseminationLog log)
        {
            var semen = await _context.BullSemens.FindAsync(log.BullSemenId);
            var cattle = await _context.Cattles.FirstAsync(c => c.EarTag == log.CattleEarTag);

            // Készlet ellenőrzés és levonás
            if (semen.StockQuantity <= 0) return BadRequest("Nincs készleten!");

            semen.StockQuantity--;
            if (semen.FirstUseDate == null) semen.FirstUseDate = log.EventDate;
            semen.LastUseDate = log.EventDate;

            // Állat adatainak frissítése
            cattle.LastInseminationDate = log.EventDate;
            cattle.InseminationBullKlsz = semen.Klsz;

            // History mentése
            _context.AnimalHistories.Add(new AnimalHistory
            {
                CattleId = cattle.Id,
                EventDate = log.EventDate,
                Type = "Termékenyítés"
            });

            _context.InseminationLogs.Add(log);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Termékenyítés sikeresen rögzítve!";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> QuickScrap(int semenId)
        {
            var semen = await _context.BullSemens.FindAsync(semenId);
            if (semen == null || semen.StockQuantity <= 0)
                return Json(new { success = false, message = "Sperma nem található vagy nincs készleten!" });

            semen.StockQuantity--;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "1 adag selejtezve.",
                newQuantity = semen.StockQuantity
            });
        }
    }
}
