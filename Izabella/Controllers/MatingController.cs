using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Controllers
{
    public class MatingController : Controller
    {
        private readonly IzabellaDbContext _context;
        public MatingController(IzabellaDbContext context) { _context = context; }

        public async Task<IActionResult> Index()
        {
            var suggestions = await _context.MatingSuggestions
                .OrderBy(s => s.CattleEarTag)
                .ThenBy(s => s.Priority)
                .ToListAsync();
            return View(suggestions);
        }

        [HttpPost]
        public async Task<IActionResult> Create(MatingSuggestion suggestion)
        {
            if (ModelState.IsValid)
            {
                // Megnézzük, van-e ilyen bika a raktárban, hogy kitölthessük a nevet
                var bull = await _context.BullSemens
                    .FirstOrDefaultAsync(b => b.Klsz == suggestion.SuggestedKlsz);

                if (bull != null) suggestion.SuggestedBullName = bull.BullName;

                _context.Add(suggestion);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Párosítási javaslat rögzítve.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var suggestion = await _context.MatingSuggestions.FindAsync(id);
            if (suggestion != null)
            {
                _context.MatingSuggestions.Remove(suggestion);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
        // MatingController.cs kiegészítés

        // Törlés a Create oldalról (visszairányít a Create-re)
        [HttpPost]
        public async Task<IActionResult> DeleteFromCreate(int id, string earTag)
        {
            var suggestion = await _context.MatingSuggestions.FindAsync(id);
            if (suggestion != null)
            {
                _context.MatingSuggestions.Remove(suggestion);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Javaslat törölve!";
            }
            // Visszaküldjük a rögzítéshez
            return RedirectToAction("Create", "Insemination", new { earTag = earTag });
        }

        // Gyors hozzáadás a Create oldalon
        [HttpPost]
        public async Task<IActionResult> QuickAdd(string earTag, string klsz, int priority)
        {
            var bull = await _context.BullSemens.FirstOrDefaultAsync(b => b.Klsz == klsz);

            var suggestion = new MatingSuggestion
            {
                CattleEarTag = earTag,
                SuggestedKlsz = klsz,
                SuggestedBullName = bull?.BullName ?? "Ismeretlen bika",
                Priority = priority
            };

            _context.MatingSuggestions.Add(suggestion);
            await _context.SaveChangesAsync();

            return RedirectToAction("Create", "Insemination", new { earTag = earTag });
        }
    }
}