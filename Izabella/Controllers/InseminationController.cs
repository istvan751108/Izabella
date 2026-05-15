using ClosedXML.Excel;
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
        [HttpGet]
        public async Task<IActionResult> InseminationReport(DateTime? startDate, DateTime? endDate, int? companyId)
        {
            // Alapértelmezés: aktuális hónap elejétől a mai napig
            var start = startDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var end = endDate ?? DateTime.Now;

            var vm = await GetInseminationReportData(start, end, companyId);
            ViewBag.AllCompaniesForFilter = await _context.Companies.OrderBy(c => c.Name).ToListAsync();

            return View(vm);
        }

        private async Task<InseminationReportVm> GetInseminationReportData(DateTime start, DateTime end, int? companyId)
        {
            var finalEndDate = end.Date.AddDays(1).AddTicks(-1);

            var vm = new InseminationReportVm
            {
                StartDate = start,
                EndDate = end,
                SelectedCompanyId = companyId
            };

            // 1. Alap logok lekérése
            var logsQuery = _context.InseminationLogs
                .Include(l => l.BullSemen)
                .Where(l => l.EventDate >= start && l.EventDate <= finalEndDate);

            // 2.Cég szerinti szűrés
            if (companyId.HasValue)
            {
                // Itt már szűrhetünk korcsoportra is, ha a cég ki van választva
                var cattleIdsForCompany = await _context.Cattles
                    .Where(c => c.CompanyId == companyId.Value && c.AgeGroup == "Tehén")
                    .Select(c => c.EarTag)
                    .ToListAsync();

                logsQuery = logsQuery.Where(l => cattleIdsForCompany.Contains(l.CattleEarTag));
            }

            var logs = await logsQuery.ToListAsync();

            // 3. Gyorsítótárazás - Itt is szűrünk korcsoportra, hogy ha NINCS cég választva, 
            // akkor is csak a tehenek kerüljenek be a riportba
            var earTags = logs.Select(l => l.CattleEarTag).Distinct().ToList();

            var cattles = await _context.Cattles
                .Where(c => earTags.Contains(c.EarTag) && c.AgeGroup == "Tehén") // KORCSOPORT SZŰRÉS
                .ToDictionaryAsync(c => c.EarTag, c => c.EnarNumber);

            var staffCodes = await _context.Staffs
                .Where(s => s.Role == StaffRole.Inszeminátor)
                .ToDictionaryAsync(s => s.Name, s => s.InseminatorCode);

            foreach (var log in logs)
            {
                // Csak akkor adjuk hozzá, ha az állat a szűrt szótárban benne van (tehát Tehén)
                if (cattles.TryGetValue(log.CattleEarTag, out string? enarNumber))
                {
                    string sType = log.BullSemen?.Type == SemenType.Fagyasztott ? "2" : "1";
                    string sMethod = log.BullSemen?.ProductionMethod == SemenProductionMethod.Mesterséges ? "1" : "2";
                    string sOrigin = log.BullSemen?.Origin == SemenOrigin.Import ? "2" : "1";

                    vm.Entries.Add(new InseminationReportEntry
                    {
                        EnarNumber = enarNumber,
                        InseminationDate = log.EventDate.Date,
                        Klsz = log.BullSemen?.Klsz ?? "",
                        BullName = log.BullSemen?.BullName ?? "",
                        Method = sMethod,
                        InseminatorCode = staffCodes.GetValueOrDefault(log.InseminatorName) ?? "00000",
                        SemenBatchNumber = log.BullSemen?.ProductionNumber ?? "-",
                        SemenType = sType,
                        SemenOrigin = sOrigin
                    });
                }
            }

            return vm;
        }

        [HttpGet]
        public async Task<IActionResult> ExportInseminationReportToExcel(DateTime startDate, DateTime endDate, int? companyId)
        {
            var vm = await GetInseminationReportData(startDate, endDate, companyId);

            // Cégadatok lekérése a fejléchez
            string companyName = "Összes állomány";
            string herdCode = "-";

            if (companyId.HasValue)
            {
                var company = await _context.Companies.FindAsync(companyId.Value);
                if (company != null)
                {
                    companyName = company.Name;

                   var herd = await _context.Herds
                        .FirstOrDefaultAsync(h => h.CompanyId == companyId.Value);

                    herdCode = herd?.HerdCode ?? "Nincs megadva";
                }
            }
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Termékenyítési Napló");

                // Cím formázása: Csak az év.hónap.nap (pl. 2024.05.11.)
                string titleDateRange = $"{startDate:yyyy.MM.dd.} - {endDate:yyyy.MM.dd.}";
                ws.Cell(1, 1).Value = $"TERMÉKENYÍTÉSI NAPLÓ ({titleDateRange})";
                ws.Range(1, 1, 1, 9).Merge().Style.Font.SetBold().Font.FontSize = 14;
                ws.Range(1, 1, 1, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(1, 1, 1, 9).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // 2. sor: Tulajdonos és Tenyészet adatok (ÚJ RÉSZ)
                ws.Cell(2, 1).Value = $"Tulajdonos: {companyName} | Tenyészetkód: {herdCode}";
                ws.Range(2, 1, 2, 9).Merge().Style.Font.SetItalic().Font.FontSize = 11;

                // Fejlécek
                string[] headers = { "Állat ENAR", "Termékenyítés dátuma", "Bika KPLSZ", "Bika név", "Term. módja", "Inszeminátor kódja", "Sperma gyártási sz.", "Sperma típusa", "Sperma eredete" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(3, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightBlue);
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }

                // Adatok
                int row = 4;
                foreach (var entry in vm.Entries.OrderBy(e => e.InseminationDate))
                {
                    ws.Cell(row, 1).Value = entry.EnarNumber;

                    // Dátum cella formázása
                    var dateCell = ws.Cell(row, 2);
                    dateCell.Value = entry.InseminationDate;
                    dateCell.Style.DateFormat.Format = "yyyy.mm.dd";

                    ws.Cell(row, 3).Value = entry.Klsz;
                    ws.Cell(row, 4).Value = entry.BullName;
                    ws.Cell(row, 5).Value = entry.Method;
                    ws.Cell(row, 6).Value = entry.InseminatorCode;
                    ws.Cell(row, 7).Value = entry.SemenBatchNumber;
                    ws.Cell(row, 8).Value = entry.SemenType;
                    ws.Cell(row, 9).Value = entry.SemenOrigin;
                    var dataRange = ws.Range(row, 1, row, 9);

                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    row++;
                }

                ws.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);

                    // Fájlnév formázása (hogy ne legyenek benne tiltott karakterek, pl. kettőspont az időből)
                    string fileName = $"Termekenyitesi_Naplo_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";

                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }
    }
}
