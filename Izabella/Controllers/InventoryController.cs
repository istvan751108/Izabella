using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace Izabella.Controllers
{
    public class InventoryController : Controller
    {
        private readonly IzabellaDbContext _context;
        public InventoryController(IzabellaDbContext context) { _context = context; }

        public async Task<IActionResult> Index(string ageGroup, string stall)
        {
            var query = _context.Cattles.Where(c => c.IsActive);

            if (!string.IsNullOrEmpty(ageGroup)) query = query.Where(c => c.AgeGroup == ageGroup);
            if (!string.IsNullOrEmpty(stall)) query = query.Where(c => c.Stall == stall);

            ViewBag.AgeGroups = await _context.Cattles.Select(c => c.AgeGroup).Distinct().ToListAsync();
            ViewBag.Stalls = await _context.Cattles.Where(c => c.Stall != null).Select(c => c.Stall).Distinct().ToListAsync();

            return View(await query.ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> SaveInventory(int[] presentIds, Dictionary<int, string> stallChanges)
        {
            if (presentIds == null || presentIds.Length == 0) return RedirectToAction(nameof(Index));

            var cattleList = await _context.Cattles.Where(c => presentIds.Contains(c.Id)).ToListAsync();

            foreach (var cattle in cattleList)
            {
                // Ha változott az istálló, frissítjük
                if (stallChanges.ContainsKey(cattle.Id) && !string.IsNullOrEmpty(stallChanges[cattle.Id]))
                {
                    cattle.Stall = stallChanges[cattle.Id];
                }

                // History bejegyzés létrehozása
                var history = new AnimalHistory
                {
                    CattleId = cattle.Id,
                    EventDate = DateTime.Now,
                    Weight = cattle.CurrentWeight,
                    OldAgeGroup = cattle.AgeGroup,
                    NewAgeGroup = cattle.AgeGroup,
                    StallName = cattle.Stall,
                    Type = "Leltár ellenőrzés"
                };

                _context.AnimalHistories.Add(history);
                _context.Update(cattle);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{presentIds.Length} állat leltározva.";
            return RedirectToAction(nameof(Index));
        }
    }
}
