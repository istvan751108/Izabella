using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Izabella.Models;
using Izabella.ViewModels;
using ClosedXML.Excel;

namespace Izabella.Controllers
{
    public class HeiferSalesController : Controller
    {
        private readonly IzabellaDbContext _context;

        public HeiferSalesController(IzabellaDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. KIGYŰJTÉS ÉS KERESÉS FELÜLETE
        // ==========================================
        public async Task<IActionResult> Index(string[] selectedMonths)
        {
            // Enyhítünk a szűrésen: minden olyan élő, aktív üszőt/tehenet nézünk,
            // akinek VAN bejegyzett utolsó termékenyítési dátuma
            var rawDates = await _context.Cattles
                .Where(c => c.IsActive && c.IsAlive && c.LastInseminationDate.HasValue)
                .Select(c => c.LastInseminationDate.Value)
                .ToListAsync();

            // Memóriában formázzuk és kiszűrjük az egyedi hónapokat
            var availableMonths = rawDates
                .Select(d => d.ToString("yyyy-MM"))
                .Distinct()
                .OrderByDescending(m => m)
                .ToList();

            ViewBag.AvailableMonths = availableMonths;
            ViewBag.SelectedMonths = selectedMonths ?? Array.Empty<string>();

            var list = new List<HeiferSalesViewModel>();

            if (selectedMonths != null && selectedMonths.Length > 0)
            {
                // Alapszűrés: Vemhes üszők, aktívak, és a kijelölt hónapokban voltak termékenyítve
                var heifers = await _context.Cattles
                    .Include(c => c.Breeding)
                    .Where(c => c.AgeGroup == "Vemhes üsző" && c.IsActive && c.LastInseminationDate.HasValue)
                    .ToListAsync();

                // Memóriában szűrjük a string formátumú hónapokat a rugalmasságért
                heifers = heifers.Where(c => selectedMonths.Contains(c.LastInseminationDate.Value.ToString("yyyy-MM"))).ToList();

                var bufferCattleIds = await _context.HeiferSalesBuffers.Select(b => b.CattleId).ToListAsync();

                foreach (var h in heifers)
                {
                    var vm = new HeiferSalesViewModel
                    {
                        CattleId = h.Id,
                        EarTag = h.EarTag,
                        EnarNumber = h.EnarNumber,
                        BirthDate = h.BirthDate,
                        LastInseminationDate = h.LastInseminationDate,
                        InseminationBullKlsz = h.InseminationBullKlsz ?? h.Breeding?.SireKlsz ?? "-",
                        FatherKlsz = h.FatherKlsz ?? "-",
                        MotherEnar = h.MotherEnar ?? "",
                        IsInBuffer = bufferCattleIds.Contains(h.Id)
                    };

                    // ANYAI ÉS NAGYANYAI ADATOK GENERÁLÁSA
                    if (!string.IsNullOrEmpty(h.MotherEnar))
                    {
                        // Megkeressük az anyát az adatbázisban az ENAR alapján
                        var mother = await _context.Cattles.FirstOrDefaultAsync(c => c.EnarNumber == h.MotherEnar);
                        if (mother != null)
                        {
                            // Anya laboreredményei
                            var damResults = await _context.MilkLabResults.Where(r => r.CattleId == mother.Id).ToListAsync();
                            CalculatedLactationData(damResults, mother, out double l1Tej, out double l1Zsir, out double l1Feh, out double l1Sejt, out double bTej, out double bZsir, out double bFeh);

                            vm.DamLact1Tej = l1Tej; vm.DamLact1Zsir = l1Zsir; vm.DamLact1Feherje = l1Feh; vm.DamLact1Sejt = l1Sejt;
                            vm.DamBestLactTej = bTej; vm.DamBestLactZsir = bZsir; vm.DamBestLactFeherje = bFeh;

                            // Anyai nagyanya megkeresése (Anya anyja)
                            if (!string.IsNullOrEmpty(mother.MotherEnar))
                            {
                                var grandDam = await _context.Cattles.FirstOrDefaultAsync(c => c.EnarNumber == mother.MotherEnar);
                                if (grandDam != null)
                                {
                                    var gDamResults = await _context.MilkLabResults.Where(r => r.CattleId == grandDam.Id).ToListAsync();
                                    CalculatedLactationData(gDamResults, grandDam, out double gl1Tej, out double gl1Zsir, out double gl1Feh, out double gl1Sejt, out double gbTej, out double gbZsir, out double gbFeh);

                                    vm.GrandDamLact1Tej = gl1Tej; vm.GrandDamLact1Zsir = gl1Zsir; vm.GrandDamLact1Feherje = gl1Feh; vm.GrandDamLact1Sejt = gl1Sejt;
                                    vm.GrandDamBestLactTej = gbTej; vm.GrandDamBestLactZsir = gbZsir; vm.GrandDamBestLactFeherje = gbFeh;
                                }
                            }
                        }
                    }
                    list.Add(vm);
                }
            }

            return View(list);
        }

        // ==========================================
        // 2. PUFFER TÁROLÓ ÉS 8 HÓNAPOS PROGNÓZIS
        // ==========================================
        public async Task<IActionResult> BufferIndex(string[] selectedBufferMonths)
        {
            // --- 1. A pufferben lévő állatok lekérése (A korábbi kódod) ---
            var bufferItems = await _context.HeiferSalesBuffers
                .Include(b => b.Cattle)
                .ThenInclude(c => c.Breeding)
                .ToListAsync();

            var availableBufferMonths = bufferItems
                .Where(b => b.Cattle.LastInseminationDate.HasValue)
                .Select(b => b.Cattle.LastInseminationDate.Value.ToString("yyyy-MM"))
                .Distinct()
                .OrderByDescending(m => m)
                .ToList();

            ViewBag.AvailableBufferMonths = availableBufferMonths;
            ViewBag.SelectedBufferMonths = selectedBufferMonths ?? Array.Empty<string>();

            if (selectedBufferMonths != null && selectedBufferMonths.Length > 0)
            {
                bufferItems = bufferItems.Where(b => b.Cattle.LastInseminationDate.HasValue &&
                    selectedBufferMonths.Contains(b.Cattle.LastInseminationDate.Value.ToString("yyyy-MM"))).ToList();
            }

            var list = new List<HeiferSalesViewModel>();
            foreach (var b in bufferItems)
            {
                var h = b.Cattle;
                var vm = new HeiferSalesViewModel
                {
                    CattleId = h.Id,
                    EarTag = h.EarTag,
                    EnarNumber = h.EnarNumber,
                    BirthDate = h.BirthDate,
                    LastInseminationDate = h.LastInseminationDate,
                    InseminationBullKlsz = h.InseminationBullKlsz ?? h.Breeding?.SireKlsz ?? "-",
                    FatherKlsz = h.FatherKlsz ?? "-",
                    MotherEnar = h.MotherEnar ?? "",
                    IsInBuffer = true
                };
                // (Anya adatok kiszámítása ha kell, az előző kód alapján...)
                list.Add(vm);
            }

            // --- 2. ÚJ RÉSZ: 8 HÓNAPOS VÁRHATÓ ELLÉS PROGNÓZIS ÖSSZEVETÉSE ---
            var today = DateTime.Today;

            // Lekérünk minden aktív, vemhes állatot a telepről
            var allPregnantCattle = await _context.Cattles
                .Where(c => c.PregnancyStatus == PregnancyStatus.Vemhes && c.LastInseminationDate != null && c.IsActive)
                .ToListAsync();

            // Lekérjük a pufferben lévő állatok ID listáját a gyors kereséshez
            var bufferCattleIds = await _context.HeiferSalesBuffers.Select(b => b.CattleId).ToListAsync();

            var forecastList = new List<ExpectedCalvingForecastViewModel>();

            // Generáljuk a következő 8 hónapot (a jelenlegi hónapot is beleértve)
            for (int i = 0; i < 8; i++)
            {
                var targetMonth = today.AddMonths(i);
                string monthStr = targetMonth.ToString("yyyy.MM");

                // Kiszűrjük azokat az állatokat, amelyeknek az ellése (Insemination + 276 nap) ebbe a hónapba esik
                var monthCalvings = allPregnantCattle
                    .Where(c => c.LastInseminationDate.Value.AddDays(276).ToString("yyyy.MM") == monthStr)
                    .ToList();

                var forecast = new ExpectedCalvingForecastViewModel
                {
                    MonthLabel = monthStr,
                    TotalExpectedCalvings = monthCalvings.Count,

                    // Vemhes üszőnek számít, ha a korcsoportja az, VAGY a jelenlegi laktációja még 0
                    HeiferCalvings = monthCalvings.Count(c => c.AgeGroup == "Vemhes üsző" || c.CurrentLactationNo == 0),

                    // Tehénnek számít mindenki más, aki már ellett legalább egyszer
                    CowCalvings = monthCalvings.Count(c => c.AgeGroup != "Vemhes üsző" && c.CurrentLactationNo > 0),

                    // Megnézzük, hogy a hónapban ellő üszők közül mennyi van jelenleg betéve a puffer tárolóba értékesítésre
                    RemovedViaBuffer = monthCalvings.Count(c => bufferCattleIds.Contains(c.Id))
                };

                forecastList.Add(forecast);
            }

            // Átadjuk a prognózis listát a ViewBag-en keresztül a felületnek
            ViewBag.CalvingForecast = forecastList;

            return View(list);
        }

        // Pufferbe adás (Ajax vagy sima Post form)
        [HttpPost]
        public async Task<IActionResult> AddToBuffer(int cattleId, string returnAction, string selectedMonths)
        {
            if (!await _context.HeiferSalesBuffers.AnyAsync(b => b.CattleId == cattleId))
            {
                _context.HeiferSalesBuffers.Add(new HeiferSalesBuffer { CattleId = cattleId });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(returnAction, new { selectedMonths = selectedMonths?.Split(',') });
        }

        // Pufferből kivétel (ha elvetélt, vagy nem felel meg)
        [HttpPost]
        public async Task<IActionResult> RemoveFromBuffer(int cattleId, string returnAction, string selectedMonths)
        {
            var item = await _context.HeiferSalesBuffers.FirstOrDefaultAsync(b => b.CattleId == cattleId);
            if (item != null)
            {
                _context.HeiferSalesBuffers.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(returnAction, new { selectedMonths = selectedMonths?.Split(',') });
        }

        // ==========================================
        // 4. EXCEL EXPORT (CLOSEDXML MENTÉS)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> ExportToExcel(int[] cattleIds)
        {
            if (cattleIds == null || cattleIds.Length == 0) return BadRequest("Nincs kiválasztott állat az exporthoz.");

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Értékesítésre felkínált üszők");

                // Fejlécek kialakítása és formázása
                string[] headers = {
                    "Fülszám", "ENAR szám", "Születési dátum", "Utolsó termék. dátum", "Bika KLSZ", "Apa KLSZ", "Anya ENAR",
                    "Anya L1 Tej (kg)", "Anya L1 Zsír %", "Anya L1 Fehérje %", "Anya L1 Sejt", "Anya Legjobb Tej", "Anya Legjobb Zsír", "Anya Legjobb Feh",
                    "Nagyanya L1 Tej", "Nagyanya L1 Zsír", "Nagyanya L1 Feh", "Nagyanya L1 Sejt", "Nagyanya Legjobb Tej", "Nagyanya Legjobb Zsír", "Nagyanya Legjobb Feh"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                int rowIdx = 2;
                foreach (var id in cattleIds)
                {
                    var h = await _context.Cattles.Include(c => c.Breeding).FirstOrDefaultAsync(c => c.Id == id);
                    if (h == null) continue;

                    // Újra kiszámoljuk az adatokat az exporthoz
                    double l1Tej = 0, l1Zsir = 0, l1Feh = 0, l1Sejt = 0, bTej = 0, bZsir = 0, bFeh = 0;
                    double gl1Tej = 0, gl1Zsir = 0, gl1Feh = 0, gl1Sejt = 0, gbTej = 0, gbZsir = 0, gbFeh = 0;

                    if (!string.IsNullOrEmpty(h.MotherEnar))
                    {
                        var mother = await _context.Cattles.FirstOrDefaultAsync(c => c.EnarNumber == h.MotherEnar);
                        if (mother != null)
                        {
                            var damResults = await _context.MilkLabResults.Where(r => r.CattleId == mother.Id).ToListAsync();
                            CalculatedLactationData(damResults, mother, out l1Tej, out l1Zsir, out l1Feh, out l1Sejt, out bTej, out bZsir, out bFeh);

                            if (!string.IsNullOrEmpty(mother.MotherEnar))
                            {
                                var gDam = await _context.Cattles.FirstOrDefaultAsync(c => c.EnarNumber == mother.MotherEnar);
                                if (gDam != null)
                                {
                                    var gDamResults = await _context.MilkLabResults.Where(r => r.CattleId == gDam.Id).ToListAsync();
                                    CalculatedLactationData(gDamResults, gDam, out gl1Tej, out gl1Zsir, out gl1Feh, out gl1Sejt, out gbTej, out gbZsir, out gbFeh);
                                }
                            }
                        }
                    }

                    worksheet.Cell(rowIdx, 1).Value = h.EarTag;
                    worksheet.Cell(rowIdx, 2).Value = h.EnarNumber;
                    worksheet.Cell(rowIdx, 3).Value = h.BirthDate.ToString("yyyy-MM-dd");
                    worksheet.Cell(rowIdx, 4).Value = h.LastInseminationDate?.ToString("yyyy-MM-dd") ?? "-";
                    worksheet.Cell(rowIdx, 5).Value = h.InseminationBullKlsz ?? h.Breeding?.SireKlsz ?? "-";
                    worksheet.Cell(rowIdx, 6).Value = h.FatherKlsz ?? "-";
                    worksheet.Cell(rowIdx, 7).Value = h.MotherEnar ?? "-";

                    // Anya L1 és Best
                    worksheet.Cell(rowIdx, 8).Value = l1Tej; worksheet.Cell(rowIdx, 9).Value = l1Zsir; worksheet.Cell(rowIdx, 10).Value = l1Feh; worksheet.Cell(rowIdx, 11).Value = l1Sejt;
                    worksheet.Cell(rowIdx, 12).Value = bTej; worksheet.Cell(rowIdx, 13).Value = bZsir; worksheet.Cell(rowIdx, 14).Value = bFeh;

                    // Nagyanya L1 és Best
                    worksheet.Cell(rowIdx, 15).Value = gl1Tej; worksheet.Cell(rowIdx, 16).Value = gl1Zsir; worksheet.Cell(rowIdx, 17).Value = gl1Feh; worksheet.Cell(rowIdx, 18).Value = gl1Sejt;
                    worksheet.Cell(rowIdx, 19).Value = gbTej; worksheet.Cell(rowIdx, 20).Value = gbZsir; worksheet.Cell(rowIdx, 21).Value = gbFeh;

                    rowIdx++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Uszo_Kinalat_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
                }
            }
        }

        // LOGIKAI SEGÉDMETÓDUS: Laktációs átlagok számítása laborvizsgálatokból
        private void CalculatedLactationData(List<MilkLabResult> results, Cattle cow,
            out double l1Tej, out double l1Zsir, out double l1Feh, out double l1Sejt,
            out double bestTej, out double bestZsir, out double bestFeh)
        {
            l1Tej = l1Zsir = l1Feh = l1Sejt = bestTej = bestZsir = bestFeh = 0;
            if (!results.Any()) return;

            // 1. Laktáció: a minták alapján (Itt feltételezzük, hogy az állat élete első 305 napos mérései adják az L1-et)
            // Egyszerűsített számítás az átlagos beltartalomra és napi tejösszegzésre:
            var lact1Samples = results.Where(r => r.BefDat <= cow.BirthDate.AddYears(3)).ToList(); // Korai tesztek
            if (lact1Samples.Any())
            {
                l1Tej = Math.Round(lact1Samples.Sum(s => s.NapiTej), 0);
                l1Zsir = Math.Round(lact1Samples.Average(s => s.NapiZsir), 2);
                l1Feh = Math.Round(lact1Samples.Average(s => s.NapiFeherje), 2);
                l1Sejt = Math.Round(lact1Samples.Average(s => s.SzomatikusSejtszam), 0);
            }

            // Legjobb laktáció kiszámítása: Évekre bontva megnézzük, mikor volt a legmagasabb a termelés
            var yearlyGroup = results.GroupBy(r => r.BefDat.Year)
                .Select(g => new
                {
                    TotalTej = g.Sum(s => s.NapiTej),
                    AvgZsir = g.Average(s => s.NapiZsir),
                    AvgFeh = g.Average(s => s.NapiFeherje)
                })
                .OrderByDescending(g => g.TotalTej)
                .FirstOrDefault();

            if (yearlyGroup != null)
            {
                bestTej = Math.Round(yearlyGroup.TotalTej, 0);
                bestZsir = Math.Round(yearlyGroup.AvgZsir, 2);
                bestFeh = Math.Round(yearlyGroup.AvgFeh, 2);
            }
        }
    }
}