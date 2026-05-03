using Izabella.Models;
using Izabella.Models.ViewModels;
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

            var cattle = await _context.Cattles
                .FirstOrDefaultAsync(c => c.EarTag.Trim() == searchEarTag.Trim());

            if (cattle == null)
            {
                TempData["Error"] = "Az állat nem található!";
                return View(new InseminationLog());
            }

            // Utolsó termékenyítés adatai a kijelzéshez
            var lastInsem = await _context.InseminationLogs
                .Include(l => l.BullSemen)
                .Where(l => l.CattleEarTag == cattle.EarTag)
                .OrderByDescending(l => l.EventDate)
                .FirstOrDefaultAsync();

            ViewBag.Cattle = cattle;
            ViewBag.LastInsem = lastInsem;

            return View(new InseminationLog { CattleEarTag = cattle.EarTag });
        }

        public async Task<IActionResult> Create(string earTag)
        {
            if (string.IsNullOrWhiteSpace(earTag))
            {
                TempData["Error"] = "Hiányzó fülszám.";
                return RedirectToAction(nameof(Index));
            }

            earTag = earTag.Trim();

            var cattle = await _context.Cattles
                .FirstOrDefaultAsync(c => c.EarTag.Trim() == earTag);

            if (cattle == null)
            {
                TempData["Error"] = $"Az állat nem található. Fülszám: '{earTag}'";
                return RedirectToAction(nameof(Index));
            }
            TempData["Success"] = $"Állat megtalálva: {cattle.EarTag}";
            // Kor ellenőrzés
            if (cattle.BirthDate > DateTime.Now.AddMonths(-10))
            {
                TempData["Error"] = $"Hiba: Az állat ({earTag}) még csak {(DateTime.Now.Year - cattle.BirthDate.Year) * 12 + DateTime.Now.Month - cattle.BirthDate.Month} hónapos. A termékenyítéshez legalább 10 hónapos kor szükséges!";
                return RedirectToAction(nameof(Index), new { searchEarTag = earTag });
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

            ViewBag.Suggestions = await _context.MatingSuggestions.Where(s => s.CattleEarTag == earTag).ToListAsync();
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

            // 1. Készlet ellenőrzés
            if (semen.StockQuantity <= 0) return BadRequest("Nincs készleten!");

            // --- ÚJ LOGIKA: Előző állapot lezárása ---
            // Ha az állat jelenleg "Vemhes" volt, de újra termékenyítjük, 
            // naplózzuk a változást a history-ba, mielőtt felülírjuk.
            if (cattle.PregnancyStatus == PregnancyStatus.Vemhes)
            {
                _context.AnimalHistories.Add(new AnimalHistory
                {
                    CattleId = cattle.Id,
                    EventDate = log.EventDate,
                    Type = "Vemhesség megszakadása",
                    Comment = "Újabb termékenyítés miatt az állapot automatikusan Üresre módosítva."
                });
            }
            // -----------------------------------------

            // 2. Készlet levonás és tranzakció
            semen.StockQuantity--;
            _context.SemenTransactions.Add(new SemenTransaction
            {
                BullSemenId = log.BullSemenId,
                Date = log.EventDate,
                Amount = 1,
                Type = log.IsReInsemination ? TransactionType.ReInsemination : TransactionType.Insemination,
                CattleEarTag = log.CattleEarTag,
                PerformedBy = log.InseminatorName
            });

            if (semen.FirstUseDate == null) semen.FirstUseDate = log.EventDate;
            semen.LastUseDate = log.EventDate;

            // 3. Állapotváltás: Most már biztosan "Nem vizsgált" lesz az új termékenyítés miatt
            cattle.PregnancyStatus = PregnancyStatus.NemVizsgált;
            cattle.LastInseminationDate = log.EventDate;
            cattle.InseminationBullKlsz = semen.Klsz;

            // 4. History mentése az új termékenyítésről
            _context.AnimalHistories.Add(new AnimalHistory
            {
                CattleId = cattle.Id,
                EventDate = log.EventDate,
                Type = "Termékenyítés"
            });

            _context.InseminationLogs.Add(log);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Termékenyítés sikeresen rögzítve! Az állat állapota: Nem vizsgált.";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> QuickScrap(int semenId, string? reason = "Selejtezés termékenyítés közben")
        {
            var semen = await _context.BullSemens.FindAsync(semenId);
            if (semen == null || semen.StockQuantity <= 0)
                return Json(new { success = false, message = "Nincs készleten!" });

            semen.StockQuantity--;

            // ÚJ: Selejt naplózása
            _context.SemenTransactions.Add(new SemenTransaction
            {
                BullSemenId = semen.Id,
                Date = DateTime.Now,
                Amount = 1,
                Type = TransactionType.Scrap,
                Comment = reason
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "1 adag selejtezve.", newQuantity = semen.StockQuantity });
        }

        public async Task<IActionResult> UsageSummary(int? year, int? month, int? day)
        {
            year ??= DateTime.Now.Year;
            month ??= DateTime.Now.Month;

            DateTime startDate = new DateTime(year.Value, month.Value, day ?? 1);
            DateTime endDate = day.HasValue ? startDate.AddDays(1) : startDate.AddMonths(1);

            var reportData = await _context.SemenTransactions
                .Where(t => t.Date >= startDate && t.Date < endDate)
                .Include(t => t.BullSemen)
                .GroupBy(t => new { t.BullSemen.Klsz, t.BullSemen.BullName })
                .Select(g => new SemenUsageReportViewModel
                {
                    Klsz = g.Key.Klsz,
                    BullName = g.Key.BullName,
                    InseminationCount = g.Count(x => x.Type == TransactionType.Insemination),
                    ReInseminationCount = g.Count(x => x.Type == TransactionType.ReInsemination),
                    ScrapCount = g.Count(x => x.Type == TransactionType.Scrap)
                }).ToListAsync();

            // EZ HIÁNYZOTT:
            ViewBag.SelectedYear = year;
            ViewBag.SelectedMonth = month;
            ViewBag.SelectedDay = day;

            return View(reportData);
        }
        public async Task<IActionResult> History(string earTag)
        {
            if (string.IsNullOrEmpty(earTag)) return NotFound();

            // Termékenyítések lekérése
            var insemLogs = await _context.InseminationLogs
                .Include(l => l.BullSemen)
                .Where(l => l.CattleEarTag == earTag)
                .OrderByDescending(l => l.EventDate)
                .ToListAsync();

            // Vizsgálati eredmények lekérése az AnimalHistory-ból
            var pregnancyTests = await _context.AnimalHistories
                .Where(h => h.Type == "Vemhességi vizsgálat" && _context.Cattles.Any(c => c.Id == h.CattleId && c.EarTag == earTag))
                .OrderByDescending(h => h.EventDate)
                .ToListAsync();

            ViewBag.EarTag = earTag;
            ViewBag.PregnancyTests = pregnancyTests;

            return View(insemLogs);
        }
        public async Task<IActionResult> Statistics(int? year, int? month)
        {
            year ??= DateTime.Now.Year;
            month ??= DateTime.Now.Month;
            var startDate = new DateTime(year.Value, month.Value, 1);
            var endDate = startDate.AddMonths(1);

            var logs = await _context.InseminationLogs
                .Where(l => l.EventDate >= startDate && l.EventDate < endDate && !l.IsReInsemination)
                .ToListAsync();

            var earTags = logs.Select(l => l.CattleEarTag).Distinct().ToList();
            var cattles = await _context.Cattles.Where(c => earTags.Contains(c.EarTag)).ToListAsync();
            var histories = await _context.AnimalHistories
                .Where(h => h.Type == "Vemhességi vizsgálat" && h.EventDate >= startDate)
                .ToListAsync();

            var stats = new InseminationStatsViewModel { Year = year.Value, Month = month.Value };
            var heiferGroups = new[] { "Növendék 9-12", "Növendék 12 hó-tól", "Vemhes üsző" };

            var markerGroups = logs.GroupBy(l => l.MarkerName ?? "Ismeretlen");

            foreach (var group in markerGroups)
            {
                var item = new MarkerStatItem { MarkerName = group.Key };
                foreach (var log in group)
                {
                    var animal = cattles.FirstOrDefault(c => c.EarTag == log.CattleEarTag);
                    if (animal == null) continue;

                    bool isHeifer = heiferGroups.Contains(animal.AgeGroup);

                    // Megnézzük, lett-e ebből a termékenyítésből vemhesség
                    // Akkor sikeres, ha van olyan history rekord, ami a termékenyítés utáni, 
                    // de a következő termékenyítés előtti, és az eredménye "Vemhes"
                    var result = histories
                        .Where(h => h.CattleId == animal.Id && h.EventDate > log.EventDate)
                        .OrderBy(h => h.EventDate)
                        .FirstOrDefault();

                    bool isSuccess = result != null && result.Comment.Contains("Vemhes");
                    // ÚJ: Ha nincs vizsgálati eredmény, akkor várólistás
                    bool isPending = result == null;

                    if (isHeifer)
                    {
                        item.HeiferInsem++;
                        if (isSuccess) item.HeiferPreg++;
                        if (isPending) item.HeiferPending++; // Add hozzá a ViewModel-hez!
                    }
                    else
                    {
                        item.CowInsem++;
                        if (isSuccess) item.CowPreg++;
                        if (isPending) item.CowPending++;
                    }
                }
                stats.MarkerStats.Add(item);
            }

            // Összesített statisztikák feltöltése a kártyákhoz
            stats.TotalInsem = stats.MarkerStats.Sum(m => m.TotalInsem);
            stats.TotalPreg = stats.MarkerStats.Sum(m => m.TotalPreg);
            stats.CowInsem = stats.MarkerStats.Sum(m => m.CowInsem);
            stats.CowPreg = stats.MarkerStats.Sum(m => m.CowPreg);
            stats.HeiferInsem = stats.MarkerStats.Sum(m => m.HeiferInsem);
            stats.HeiferPreg = stats.MarkerStats.Sum(m => m.HeiferPreg);

            return View(stats);
        }
    }
}
