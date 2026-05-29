using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Globalization;

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

        [HttpPost]
        public async Task<IActionResult> ImportAfiPregnancy(IFormFile file, string fileType)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Nincs fájl kiválasztva.";
                return RedirectToAction(nameof(Index));
            }

            int updatedCount = 0;
            int skippedCount = 0;

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                string content = await reader.ReadToEndAsync();
                string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                int dataStartIndex = Array.IndexOf(lines, "DATA");
                if (dataStartIndex == -1)
                {
                    TempData["Error"] = "Érvénytelen DIF fájlformátum (hiányzik a DATA blokk).";
                    return RedirectToAction(nameof(Index));
                }

                int botsFound = 0;

                // Lekérjük a jelenleg vizsgálatra váró állatokat a memóriába a gyorsabb kereséshez
                var waitingCattle = await _context.Cattles
                    .Where(c => c.PregnancyStatus == PregnancyStatus.NemVizsgált ||
                                c.PregnancyStatus == PregnancyStatus.Visszaivarzott)
                    .ToListAsync();

                for (int i = dataStartIndex; i < lines.Length; i++)
                {
                    if (lines[i].Trim() == "BOT")
                    {
                        botsFound++;
                        if (botsFound <= 2) continue; // Első két fejléc blokk átugrása

                        var rawData = ExtractAfimilkData(lines, i);

                        string rawEarTag = "";
                        string rawInsemDate = "";

                        // Beállítjuk az indexeket a kiválasztott fájltípus alapján
                        if (fileType == "Tehén" && rawData.Count >= 9)
                        {
                            rawEarTag = rawData[1];
                            rawInsemDate = rawData[8];
                        }
                        else if (fileType == "Üsző" && rawData.Count >= 12)
                        {
                            rawEarTag = rawData[1];
                            rawInsemDate = rawData[11];
                        }

                        if (!string.IsNullOrEmpty(rawEarTag) && !string.IsNullOrEmpty(rawInsemDate))
                        {
                            string formattedEarTag = FormatEarTag(rawEarTag);

                            // Dátum konvertálása (Afimilk formátum: yyyy/MM/dd)
                            if (DateTime.TryParseExact(rawInsemDate.Replace("-", "/"), "yyyy/MM/dd",
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime afiInsemDate))
                            {
                                // Megkeressük az állatot a várakozók között fülszám ÉS az utolsó regisztrált termékenyítési dátum alapján
                                var cattle = waitingCattle.FirstOrDefault(c =>
                                    c.EarTag == formattedEarTag &&
                                    c.LastInseminationDate.HasValue &&
                                    c.LastInseminationDate.Value.Date == afiInsemDate.Date);

                                if (cattle != null)
                                {
                                    // Vemhességi adatok rögzítése
                                    cattle.PregnancyStatus = PregnancyStatus.Vemhes;
                                    cattle.LastPregnancyTestDate = DateTime.Now;

                                    // Történet (AnimalHistory) mentése
                                    _context.AnimalHistories.Add(new AnimalHistory
                                    {
                                        CattleId = cattle.Id,
                                        EventDate = DateTime.Now,
                                        Type = "Vemhességi vizsgálat",
                                        Comment = $"Automatikus elbírálás Afimilk importból (Vemhesült term. dátuma: {afiInsemDate:yyyy-MM-dd})"
                                    });

                                    updatedCount++;
                                }
                                else
                                {
                                    skippedCount++;
                                }
                            }
                        }
                    }
                }
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Sikeresen feldolgozva! { updatedCount} db állat státusza frissült VEMHES - re az Afimilk adatok alapján.";
            }
            else
            {
                TempData["Info"] = "A fájl feldolgozása megtörtént, de nem találtunk egyezést a listában szereplő fülszámok és termékenyítési dátumok alapján.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Újrahasznosított Afimilk DIF struktúra elemző
        private List<string> ExtractAfimilkData(string[] lines, int start)
        {
            var result = new List<string>();
            int j = start + 1;

            while (j < lines.Length && lines[j] != "BOT" && lines[j] != "-1,0")
            {
                string line = lines[j].Trim();

                if (line.StartsWith("0,") && line.Length > 2)
                {
                    result.Add(line.Substring(2).Trim('"'));
                }
                else if (line.StartsWith("\"") && line.EndsWith("\""))
                {
                    result.Add(line.Trim('"'));
                }
                else if (j > 0 && (lines[j - 1] == "V" || lines[j - 1] == "1,0"))
                {
                    if (line != "V" && line != "1,0" && line != "0,0")
                    {
                        result.Add(line.Trim('"'));
                    }
                }
                j++;
            }
            return result;
        }

        // Fülszám formázó (L5 -> L005 -> L0005 vagy T9891 -> T9891 hossztól függően, kiegészítve 4-es helyiértékre)
        private string FormatEarTag(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string clean = raw.Replace("\"", "").Trim();
            if (clean.Length < 2) return clean;

            char letter = clean[0];
            // Ha az első karakter betű, a többi szám, akkor kiegészítjük 0-kal legalább 4 karakteres számmá (pl. L5 -> L0005 helyett L0005 a rendszered szerint)
            // Megjegyzés: Ha nálad az L005 a cél (3 db nulla), akkor a :D4-et cseréld :D3-ra, de a tejimportból kiindulva a :D4 a stabil megoldás
            return int.TryParse(clean.Substring(1), out int n) ? $"{letter}{n:D4}" : clean;
        }
    }
}