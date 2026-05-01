using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Controllers
{
    public class PregnancyController : Controller
    {
        private readonly IzabellaDbContext _context;

        public PregnancyController(IzabellaDbContext context)
        {
            _context = context;
        }

        // VEX Várólista
        public async Task<IActionResult> Index()
        {
            // Csak a 'Nem vizsgált' és 'Visszaivarzott' státuszú állatokat listázzuk
            var toTest = await _context.Cattles
                .Where(c => c.PregnancyStatus == PregnancyStatus.NemVizsgált ||
                            c.PregnancyStatus == PregnancyStatus.Visszaivarzott)
                .OrderBy(c => c.LastInseminationDate)
                .ToListAsync();

            return View(toTest);
        }

        [HttpPost]
        public async Task<IActionResult> RecordResult(int cattleId, PregnancyStatus result)
        {
            var cattle = await _context.Cattles.FindAsync(cattleId);
            if (cattle == null) return NotFound();

            cattle.PregnancyStatus = result;
            cattle.LastPregnancyTestDate = DateTime.Now;

            // AUTOMATIKUS JAVASLAT GENERÁLÁSA
            if (result == PregnancyStatus.Üres || result == PregnancyStatus.Visszaivarzott)
            {
                // Megkeressük az utolsó termékenyítését
                var lastInsem = await _context.InseminationLogs
    .Include(l => l.BullSemen) // Ez elengedhetetlen a BullSemen adatok eléréséhez!
    .Where(l => l.CattleEarTag == cattle.EarTag)
    .OrderByDescending(l => l.EventDate)
    .FirstOrDefaultAsync();

                if (lastInsem != null && lastInsem.BullSemen != null) // Extra biztonsági ellenőrzés
                {
                    // Töröljük a régi javaslatokat
                    var oldSuggestions = _context.MatingSuggestions.Where(s => s.CattleEarTag == cattle.EarTag);
                    _context.MatingSuggestions.RemoveRange(oldSuggestions);

                    // Új javaslat az utolsó bika alapján
                    _context.MatingSuggestions.Add(new MatingSuggestion
                    {
                        CattleEarTag = cattle.EarTag,
                        SuggestedKlsz = lastInsem.BullSemen.Klsz,
                        SuggestedBullName = lastInsem.BullSemen.BullName,
                        Priority = 1,
                        CreatedDate = DateTime.Now
                    });
                }
            }

            // History mentése a megjegyzéssel
            _context.AnimalHistories.Add(new AnimalHistory
            {
                CattleId = cattle.Id,
                EventDate = DateTime.Now,
                Type = "Vemhességi vizsgálat",
                Comment = $"Eredmény: {result}" // Itt már használjuk az új mezőt
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{cattle.EarTag} eredménye rögzítve ({result}). Javaslat frissítve.";

            return RedirectToAction(nameof(Index));
        }
    }
}
