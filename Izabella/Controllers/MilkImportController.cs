using ClosedXML.Excel;
using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Izabella.Controllers
{
    public class MilkImportController : Controller
    {
        private readonly IzabellaDbContext _context;

        public MilkImportController(IzabellaDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Import() => View();

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Nincs fájl kiválasztva.");

            DateTime fileDate;
            try
            {
                var match = Regex.Match(file.FileName, @"(\d{4}-\d{2}-\d{2})");
                fileDate = DateTime.Parse(match.Value).AddDays(-1);
            }
            catch
            {
                fileDate = DateTime.Today.AddDays(-1);
            }

            var stagingList = new List<MilkDataStaging>();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                string content = await reader.ReadToEndAsync();
                string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                int dataStartIndex = Array.IndexOf(lines, "DATA");
                int botsFound = 0;

                // Az Afimilk-nél egy rekord 15 darab adatot tartalmaz (BOT és -1,0 között)
                for (int i = dataStartIndex; i < lines.Length; i++)
                {
                    if (lines[i].Trim() == "BOT")
                    {
                        botsFound++;
                        if (botsFound <= 2) continue; // Első két fejléc blokk átugrása

                        // Az adatok sorrendje: 0:Sorszám, 1:Fülszám, 2:Istálló, 3:Lakt, 4:Nap, 5:ENAR...
                        // A DIF-ben minden adat előtt van egy 'V' vagy '1,0' vagy '0,x'. 
                        // Kigyűjtjük a tiszta adatokat:
                        var rawData = ExtractAfimilkData(lines, i);

                        if (rawData.Count >= 15)
                        {
                            var entry = new MilkDataStaging
                            {
                                RecordDate = fileDate,
                                ImportTimestamp = DateTime.Now,
                                EarTag = FormatEarTag(rawData[1]),
                                BarnId = rawData[2],
                                LactationNo = int.TryParse(rawData[3], out int l) ? l : 0,
                                DaysInMilk = int.TryParse(rawData[4], out int d) ? d : 0,
                                Enar = FormatEnar(rawData[5]),
                                Yield1 = ParseDouble(rawData[6]),
                                Yield2 = ParseDouble(rawData[7]),
                                Yield3 = ParseDouble(rawData[8]),
                                Fat1 = ParseDouble(rawData[9]),
                                Fat2 = ParseDouble(rawData[10]),
                                Fat3 = ParseDouble(rawData[11]),
                                Protein1 = ParseDouble(rawData[12]),
                                Protein2 = ParseDouble(rawData[13]),
                                Protein3 = ParseDouble(rawData[14])
                            };
                            stagingList.Add(entry);
                        }
                    }
                }
            }

            _context.MilkDataStagings.AddRange(stagingList);
            await _context.SaveChangesAsync();
            return RedirectToAction("StagingList");
        }

        private List<string> ExtractAfimilkData(string[] lines, int start)
        {
            var result = new List<string>();
            int j = start + 1;

            // Addig megyünk, amíg a következő rekordig vagy a fájl végéig nem érünk
            while (j < lines.Length && lines[j] != "BOT" && lines[j] != "-1,0")
            {
                string line = lines[j].Trim();

                // 1. ESET: Az adat egy sorban van a jelzővel, pl: 0,12.5 vagy 0,"L15"
                if (line.StartsWith("0,") && line.Length > 2)
                {
                    result.Add(line.Substring(2).Trim('"'));
                }
                // 2. ESET: Az adat idézőjelek között van a következő sorban
                else if (line.StartsWith("\"") && line.EndsWith("\""))
                {
                    result.Add(line.Trim('"'));
                }
                // 3. ESET: Ha az előző sor V vagy 1,0 volt, és a mostani sor egy tiszta szám vagy szöveg
                else if (j > 0 && (lines[j - 1] == "V" || lines[j - 1] == "1,0"))
                {
                    // Csak akkor adjuk hozzá, ha nem egy másik vezérlőkód
                    if (line != "V" && line != "1,0" && line != "0,0")
                    {
                        result.Add(line.Trim('"'));
                    }
                }
                j++;
            }
            return result;
        }

        private string FormatEnar(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "0") return "";
            // Tisztítás: csak a számok maradjanak meg
            string clean = Regex.Replace(raw, "[^0-9]", "");
            if (string.IsNullOrEmpty(clean)) return "";
            return "HU" + clean;
        }

        private string FormatEarTag(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string clean = raw.Replace("\"", "").Trim();
            if (clean.Length < 2) return clean;
            char letter = clean[0];
            return int.TryParse(clean.Substring(1), out int n) ? $"{letter}{n:D4}" : clean;
        }

        private double? ParseDouble(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "--" || s == "0") return null;
            // Az Afimilk tizedesvesszőt használ, a Double.Parse-nak pont kellhet kultúrától függően
            string normalized = s.Replace(',', '.');
            if (double.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d))
                return d;
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> StagingList()
        {
            var data = await _context.MilkDataStagings.Where(s => !s.IsProcessed)
                .OrderByDescending(s => s.RecordDate).ThenBy(s => s.EarTag).ToListAsync();
            return View(data);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStaging(int id)
        {
            var item = await _context.MilkDataStagings.FindAsync(id);
            if (item != null)
            {
                _context.MilkDataStagings.Remove(item);
                await _context.SaveChangesAsync();
            }

            // Visszatérünk ugyanoda, ahol voltunk
            return RedirectToAction(nameof(StagingList));
        }
        [HttpPost]
        public async Task<IActionResult> ApproveAll(double totalMeasuredMilk, string quarantineBarn, string medicatedBarn)
        {
            var stagingData = await _context.MilkDataStagings.Where(s => !s.IsProcessed).ToListAsync();
            if (!stagingData.Any()) return RedirectToAction(nameof(StagingList));

            DateTime reportDate = stagingData.First().RecordDate;
            // --- VÉDELEM: Duplikáció ellenőrzése ---
            bool alreadyExists = await _context.MilkProductions.AnyAsync(p => p.Date.Date == reportDate.Date);
            if (alreadyExists)
            {
                TempData["ErrorMessage"] = $"Hiba: Erre a napra ({reportDate:yyyy-MM-dd}) már töltöttél fel adatokat! Ha újra akarod tölteni, előbb töröld a napi adatokat.";
                return RedirectToAction(nameof(StagingList));
            }
            // --- VÉDELEM VÉGE ---
            double identifiedMilkSum = 0;
            var processedCattleIds = new List<int>();

            // 1. BEAZONOSÍTOTT ADATOK MENTÉSE
            foreach (var staging in stagingData)
            {
                var cattle = await _context.Cattles.FirstOrDefaultAsync(c => c.EarTag == staging.EarTag);
                if (cattle != null)
                {
                    identifiedMilkSum += (staging.Yield1 ?? 0) + (staging.Yield2 ?? 0) + (staging.Yield3 ?? 0);
                    // 3. Istálló frissítése, ha változott
                    if (cattle.Stall != staging.BarnId)
                    {
                        _context.AnimalHistories.Add(new AnimalHistory
                        {
                            CattleId = cattle.Id,
                            EventDate = staging.RecordDate,
                            OldAgeGroup = cattle.AgeGroup,
                            NewAgeGroup = cattle.AgeGroup,
                            StallName = staging.BarnId,
                            Type = "Áthelyezés",
                            Comment = $"Automatikus áthelyezés tejimport során ({cattle.Stall} -> {staging.BarnId})"
                        });
                        cattle.Stall = staging.BarnId;
                    }

                    // 4. Új végleges tejadat létrehozása
                    var production = new MilkProduction
                    {
                        CattleId = cattle.Id,
                        Date = staging.RecordDate,
                        LactationNo = staging.LactationNo,
                        DaysInMilk = staging.DaysInMilk,
                        Yield1 = staging.Yield1 ?? 0,
                        Yield2 = staging.Yield2 ?? 0,
                        Yield3 = staging.Yield3 ?? 0,
                        Fat1 = staging.Fat1,
                        Fat2 = staging.Fat2,
                        Fat3 = staging.Fat3,
                        Protein1 = staging.Protein1,
                        Protein2 = staging.Protein2,
                        Protein3 = staging.Protein3,
                        IsEstimated = false
                    };

                    _context.MilkProductions.Add(production);
                    processedCattleIds.Add(cattle.Id);
                    staging.IsProcessed = true;
                }
            }

            // 2. SÚLYOZOTT KIEGYENLÍTÉS PONTOSÍTÁSA
            double missingMilkTotal = Math.Round(totalMeasuredMilk - identifiedMilkSum, 1);

            if (missingMilkTotal > 0)
            {
                var missingCows = await _context.Cattles
                    .Where(c => c.IsActive && c.AgeGroup == "Tehén" && !processedCattleIds.Contains(c.Id))
                    .Where(c => c.Stall != quarantineBarn && c.Stall != medicatedBarn)
                    .ToListAsync();

                if (missingCows.Any())
                {
                    var cowWeights = new List<(Cattle Cow, double Weight)>();
                    double totalWeightPoints = 0;

                    foreach (var cow in missingCows)
                    {
                        double avg = await GetLastWeekAverage(cow.Id);
                        cowWeights.Add((cow, avg));
                        totalWeightPoints += avg;
                    }

                    // Sorbarendezzük átlag szerint csökkenőbe, hogy a "maradékot" a nagyok kapják
                    cowWeights = cowWeights.OrderByDescending(x => x.Weight).ToList();

                    double distributedSoFar = 0;
                    var productionsToInsert = new List<MilkProduction>();

                    for (int i = 0; i < cowWeights.Count; i++)
                    {
                        var current = cowWeights[i];
                        double share = (current.Weight / totalWeightPoints) * missingMilkTotal;
                        double roundedShare = Math.Round(share, 1);

                        // Ha az utolsó elemnél tartunk, a kerekítési hibák miatt a maradékot hozzáadjuk
                        if (i == cowWeights.Count - 1)
                        {
                            roundedShare = Math.Round(missingMilkTotal - distributedSoFar, 1);
                        }

                        distributedSoFar += roundedShare;

                        productionsToInsert.Add(new MilkProduction
                        {
                            CattleId = current.Cow.Id,
                            Date = reportDate,
                            Yield1 = roundedShare,
                            IsEstimated = true,
                            LactationNo = current.Cow.CurrentLactationNo,
                            Comment = "Súlyozott átlag alapján pótolva"
                        });
                    }
                    _context.MilkProductions.AddRange(productionsToInsert);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(StagingList));
        }

        // SEGÉDMETÓDUS AZ ÁTLAGSZÁMÍTÁSHOZ
        private async Task<double> GetLastWeekAverage(int cattleId)
        {
            var lastProductions = await _context.MilkProductions
                .Where(p => p.CattleId == cattleId)
                .OrderByDescending(p => p.Date)
                .Take(7)
                .ToListAsync();

            if (!lastProductions.Any()) return 25.0;

            // JAVÍTVA: Itt nem kell a ?? mert a Yield-ek nem nullable típusok a modellben
            return lastProductions.Average(p => p.Yield1 + p.Yield2 + p.Yield3);
        }

        public async Task<IActionResult> MonthlyPivot(int? year, int? month, MilkReportType type = MilkReportType.Yield)
        {
            // Ahelyett, hogy itt újra leírnád a logikát, hívd meg a közös metódust!
            var viewModel = await GetMonthlyPivotData(year, month, type);
            return View(viewModel);
        }
        [HttpGet]
        public async Task<IActionResult> ExportMonthlyPivotToExcel(int year, int month, MilkReportType type)
        {
            // Itt már a javított, paraméterezett metódust hívjuk
            var vm = await GetMonthlyPivotData(year, month, type);

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Tejtermelés");

                // CÍMSOR dinamikus megnevezéssel
                string reportName = type == MilkReportType.Fat ? "TEJZSÍR %" :
                                   type == MilkReportType.Protein ? "TEJFEHÉRJE %" : "TEJTERMELÉS (kg)";

                ws.Cell(1, 1).Value = $"HAVI {reportName} NAPLÓ - {year}. {month:D2}";
                int lastCol = (vm.DaysInMonth.Count * 3) + 2;
                var titleRange = ws.Range(1, 1, 1, lastCol);
                titleRange.Merge().Style.Font.SetBold().Font.FontSize = 16;
                titleRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                // FEJLÉC - 1. sor (Fülszám és Napok száma)
                ws.Cell(3, 1).Value = "Fülszám";
                ws.Range(3, 1, 4, 1).Merge().Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center).Font.SetBold();

                int colIdx = 2;
                foreach (var day in vm.DaysInMonth)
                {
                    var dayRange = ws.Range(3, colIdx, 3, colIdx + 2);
                    dayRange.Merge().Value = $"{day}. nap";
                    dayRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Font.SetBold();
                    dayRange.Style.Fill.SetBackgroundColor(XLColor.LightSkyBlue).Border.OutsideBorder = XLBorderStyleValues.Thin;

                    // EXTRA: Itt állítsunk be egy fix szélességet a három al-oszlopnak
                    ws.Columns(colIdx, colIdx + 2).Width = 6.5; // Próbáld 5 és 7 között, amíg szép nem lesz

                    colIdx += 3;
                }

                ws.Cell(3, colIdx).Value = "Havi Átlag";
                ws.Range(3, colIdx, 4, colIdx).Merge().Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center).Font.SetBold();

                // ADATOK KIÍRÁSA - Dinamikus oszlopkezelés
                int rowIdx = 5;
                foreach (var row in vm.Rows)
                {
                    ws.Cell(rowIdx, 1).Value = row.EarTag;
                    ws.Cell(rowIdx, 1).Style.Font.SetBold();

                    colIdx = 2;
                    foreach (var day in vm.DaysInMonth)
                    {
                        var data = row.DailyData[day];
                        if (data.HasData)
                        {
                            if (type == MilkReportType.Yield)
                            {
                                ws.Cell(rowIdx, colIdx).Value = data.Y1;
                                ws.Cell(rowIdx, colIdx + 1).Value = data.Y2;
                                ws.Cell(rowIdx, colIdx + 2).Value = data.Y3;
                            }
                            else
                            {
                                // Zsír/Fehérje esetén összevonjuk a 3 cellát és középre zárjuk
                                var cellRange = ws.Range(rowIdx, colIdx, rowIdx, colIdx + 2);
                                cellRange.Merge().Value = data.Y1; // A GetMonthlyPivotData már ide tette az átlagot
                                cellRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                                cellRange.Style.NumberFormat.Format = "0.00";
                            }
                        }
                        colIdx += 3;
                    }
                    ws.Cell(rowIdx, colIdx).Value = row.RowAverage;
                    ws.Cell(rowIdx, colIdx).Style.Font.SetBold();
                    ws.Cell(rowIdx, colIdx).Style.NumberFormat.Format = (type == MilkReportType.Yield ? "0.0" : "0.00");
                    rowIdx++;
                }

                // ALSÓ ÖSSZESÍTŐ (Napi Teljes / Átlag)
                ws.Cell(rowIdx, 1).Value = type == MilkReportType.Yield ? "Napi Teljes:" : "Napi Átlag %:";
                ws.Cell(rowIdx, 1).Style.Font.SetBold();

                int finalColIdx = 2;
                foreach (var day in vm.DaysInMonth)
                {
                    var range = ws.Range(rowIdx, finalColIdx, rowIdx, finalColIdx + 2);
                    range.Merge().Value = vm.DailyTotals[day].DayTotal;
                    range.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
                    range.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    range.Style.NumberFormat.Format = (type == MilkReportType.Yield ? "#,##0" : "0.00");
                    finalColIdx += 3;
                }

                // ESZTÉTIKA - FONTOS SORREND:

                // 1. Minden adat-oszlopnak (2-től az utolsó előttiig) adjunk fix szélességet
                // Ez biztosítja, hogy a ". nap" feliratok és a számok elférjenek
                ws.Columns(2, lastCol - 1).Width = 7.0;

                // 2. A kiemelt oszlopok egyedi szélessége
                ws.Column(1).Width = 15;        // Fülszám
                ws.Column(lastCol).Width = 12;  // Havi átlag

                // 3. Rácsok és keretek
                var tableRange = ws.Range(3, 1, rowIdx, lastCol);
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                // SZIGORÚAN TILOS: ws.Columns().AdjustToContents(); 
                // Ha ezt meghívod, az összes fenti szélesség beállítást elrontja!

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Tej_Riport_{year}_{month}.xlsx");
                }
            }
        }
        private async Task<MonthlyPivotViewModel> GetMonthlyPivotData(int? year, int? month, MilkReportType type = MilkReportType.Yield)
        {
            int rYear = year ?? DateTime.Today.Year;
            int rMonth = month ?? DateTime.Today.Month;
            var startDate = new DateTime(rYear, rMonth, 1);
            var daysInMonth = Enumerable.Range(1, DateTime.DaysInMonth(rYear, rMonth)).ToList();

            var productions = await _context.MilkProductions
                .Include(p => p.Cattle)
                .Where(p => p.Date.Year == rYear && p.Date.Month == rMonth)
                .ToListAsync();

            return new MonthlyPivotViewModel
            {
                ReportType = type,
                SelectedMonth = startDate,
                DaysInMonth = daysInMonth,
                Rows = productions.GroupBy(p => p.Cattle.EarTag).Select(g => new CattleMilkRow
                {
                    EarTag = g.Key,
                    DailyData = daysInMonth.ToDictionary(d => d, d => {
                        var p = g.FirstOrDefault(x => x.Date.Day == d);
                        double val = 0;

                        if (p != null)
                        {
                            if (type == MilkReportType.Fat)
                            {
                                // Zsír átlagolása a meglévő fejések alapján
                                var fats = new List<double?> { p.Fat1, p.Fat2, p.Fat3 }.Where(f => f.HasValue).Select(f => f.Value);
                                val = fats.Any() ? fats.Average() : 0;
                            }
                            else if (type == MilkReportType.Protein)
                            {
                                // Fehérje átlagolása a meglévő fejések alapján
                                var proteins = new List<double?> { p.Protein1, p.Protein2, p.Protein3 }.Where(pr => pr.HasValue).Select(pr => pr.Value);
                                val = proteins.Any() ? proteins.Average() : 0;
                            }
                            else
                            {
                                val = p.TotalYield;
                            }
                        }

                        return new DayProduction
                        {
                            // Ha tejmennyiség, akkor az 1. fejést is látni akarjuk a cellában, 
                            // de beltartalomnál csak a napi átlagértéket
                            Y1 = (type == MilkReportType.Yield && p != null) ? p.Yield1 : val,
                            Y2 = (type == MilkReportType.Yield && p != null) ? p.Yield2 : 0,
                            Y3 = (type == MilkReportType.Yield && p != null) ? p.Yield3 : 0,
                            HasData = p != null
                        };
                    })
                }).OrderBy(r => r.EarTag).ToList(),

                DailyTotals = daysInMonth.ToDictionary(d => d, d => {
                    var dayProds = productions.Where(p => p.Date.Day == d).ToList();

                    if (type == MilkReportType.Yield)
                    {
                        return new DailyTotal
                        {
                            SumY1 = dayProds.Sum(p => p.Yield1),
                            SumY2 = dayProds.Sum(p => p.Yield2),
                            SumY3 = dayProds.Sum(p => p.Yield3)
                        };
                    }
                    else
                    {
                        // Zsír vagy Fehérje esetén a nap átlagát számoljuk ki
                        double avgValue = 0;
                        if (dayProds.Any())
                        {
                            if (type == MilkReportType.Fat)
                            {
                                avgValue = dayProds.Average(p =>
                                    new List<double?> { p.Fat1, p.Fat2, p.Fat3 }.Where(f => f.HasValue).Select(f => f.Value).DefaultIfEmpty(0).Average());
                            }
                            else
                            {
                                avgValue = dayProds.Average(p =>
                                    new List<double?> { p.Protein1, p.Protein2, p.Protein3 }.Where(pr => pr.HasValue).Select(pr => pr.Value).DefaultIfEmpty(0).Average());
                            }
                        }

                        // Trükk: a napi átlagot a SumY1-be tesszük, a többi 0, 
                        // így a DayTotal (ami a hármat összeadja) a helyes átlagot fogja mutatni a diagramon
                        return new DailyTotal { SumY1 = avgValue, SumY2 = 0, SumY3 = 0 };
                    }
                })
            };
        }
        public async Task<IActionResult> LactationReport(int? year, int? month)
        {
            int rYear = year ?? DateTime.Today.Year;

            // Lekérdezés alapja: az adott év adatai
            var query = _context.MilkProductions
                .Where(p => p.Date.Year == rYear);

            // Ha van hónap, szűrünk rá, ha nincs, marad az egész év
            if (month.HasValue && month.Value > 0)
            {
                query = query.Where(p => p.Date.Month == month.Value);
            }

            var data = await query.ToListAsync();

            // Csoportosítás (mivel egy évben egy tehén válthat laktációt, 
            // a legutolsó rögzített laktációját vesszük alapul az időszakban)
            var groups = data
                .GroupBy(p => p.CattleId)
                .Select(g => new
                {
                    CattleId = g.Key,
                    // Az időszak utolsó rögzített laktációs száma
                    Lactation = g.OrderByDescending(p => p.Date).First().LactationNo,
                    AvgYield = g.Average(p => p.TotalYield)
                })
                .GroupBy(x => x.Lactation)
                .Select(lg => new LactationGroupData
                {
                    LactationNumber = lg.Key,
                    Count = lg.Count(),
                    AvgDailyYield = lg.Average(x => x.AvgYield)
                })
                .OrderBy(o => o.LactationNumber)
                .ToList();

            int totalCows = groups.Sum(g => g.Count);
            foreach (var g in groups)
            {
                g.Percentage = totalCows > 0 ? (double)g.Count / totalCows * 100 : 0;
            }

            var vm = new LactationReportViewModel
            {
                SelectedMonth = new DateTime(rYear, month ?? 1, 1),
                IsYearlyView = !month.HasValue, // Új property a ViewModelbe
                Groups = groups,
                TotalCows = totalCows
            };

            return View(vm);
        }
        public async Task<IActionResult> PerformanceReport()
        {
            var allProductions = await _context.MilkProductions
                .Include(p => p.Cattle)
                .ToListAsync();

            var cattleGroups = allProductions.GroupBy(p => p.Cattle.EarTag);

            var reportData = cattleGroups.Select(g => {
                var lactations = g.GroupBy(p => p.LactationNo)
                    .Select(l => new LactationSummary
                    {
                        LactationNo = l.Key,
                        TotalYield = l.Sum(p => p.TotalYield),
                        DaysInMilk = l.Select(p => p.Date.Date).Distinct().Count(),
                        DailyAverage = l.Sum(p => p.TotalYield) / l.Select(p => p.Date.Date).Distinct().Count()
                    }).ToList();

                var bestLact = lactations.OrderByDescending(l => l.TotalYield).FirstOrDefault();

                return new CattlePerformanceViewModel
                {
                    EarTag = g.Key,
                    Lactations = lactations,
                    TotalLifeYield = lactations.Sum(l => l.TotalYield),
                    OverallDailyAverage = lactations.Average(l => l.DailyAverage),
                    BestLactationNumber = bestLact?.LactationNo ?? 0,
                    BestLactationYield = bestLact?.TotalYield ?? 0
                };
            }).ToList();

            // TOP 10 - Egy laktációban elért legtöbb tej
            var topByLactation = allProductions.GroupBy(p => new { p.Cattle.EarTag, p.LactationNo })
                .Select(g => new CattleRankItem
                {
                    EarTag = g.Key.EarTag,
                    LactationNo = g.Key.LactationNo,
                    TotalYield = g.Sum(p => p.TotalYield),
                    DailyAverage = g.Sum(p => p.TotalYield) / g.Select(p => p.Date.Date).Distinct().Count()
                })
                .OrderByDescending(x => x.TotalYield)
                .Take(10)
                .ToList();

            // TOP 10 - Életteljesítmény
            var topByLifetime = reportData
                .OrderByDescending(x => x.TotalLifeYield)
                .Take(10)
                .Select(x => new CattleRankItem
                {
                    EarTag = x.EarTag,
                    TotalYield = x.TotalLifeYield,
                    DailyAverage = x.OverallDailyAverage
                }).ToList();

            var vm = new TopPerformanceViewModel
            {
                TopByLactation = topByLactation,
                TopByLifetime = topByLifetime,
                AllCattlePerformances = reportData
            };

            return View(vm);
        }
        [HttpGet]
        public async Task<IActionResult> ExportPerformanceToExcel()
        {
            // Itt ugyanazt a logikát használjuk, mint a PerformanceReport-nál
            var allProductions = await _context.MilkProductions.Include(p => p.Cattle).ToListAsync();
            var cattleGroups = allProductions.GroupBy(p => p.Cattle.EarTag);

            var reportData = cattleGroups.Select(g =>
            {
                var lactations = g.GroupBy(p => p.LactationNo)
                    .Select(l => new LactationSummary
                    {
                        LactationNo = l.Key,
                        TotalYield = l.Sum(p => p.TotalYield),
                        DailyAverage = l.Sum(p => p.TotalYield) / l.Select(p => p.Date.Date).Distinct().Count()
                    }).ToList();

                var bestLact = lactations.OrderByDescending(l => l.TotalYield).FirstOrDefault();

                return new CattlePerformanceViewModel
                {
                    EarTag = g.Key,
                    Lactations = lactations,
                    TotalLifeYield = lactations.Sum(l => l.TotalYield),
                    OverallDailyAverage = lactations.Average(l => l.DailyAverage),
                    BestLactationNumber = bestLact?.LactationNo ?? 0,
                    BestLactationYield = bestLact?.TotalYield ?? 0
                };
            }).OrderBy(x => x.EarTag).ToList();

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Életteljesítmény");

                // Fejléc
                ws.Cell(1, 1).Value = "Fülszám";
                ws.Cell(1, 2).Value = "Laktációk száma";
                ws.Cell(1, 3).Value = "Legjobb laktáció sorszáma";
                ws.Cell(1, 4).Value = "Legjobb laktáció (kg)";
                ws.Cell(1, 5).Value = "Összes termelt tej (kg)";
                ws.Cell(1, 6).Value = "Életút napi átlag (kg)";

                var headerRange = ws.Range(1, 1, 1, 6);
                headerRange.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightSlateGray).Font.SetFontColor(XLColor.White);

                // Adatok
                int rowIdx = 2;
                foreach (var item in reportData)
                {
                    ws.Cell(rowIdx, 1).Value = item.EarTag;
                    ws.Cell(rowIdx, 2).Value = item.Lactations.Count;
                    ws.Cell(rowIdx, 3).Value = item.BestLactationNumber;
                    ws.Cell(rowIdx, 4).Value = item.BestLactationYield;
                    ws.Cell(rowIdx, 5).Value = item.TotalLifeYield;
                    ws.Cell(rowIdx, 6).Value = item.OverallDailyAverage;

                    // Formázás
                    ws.Cell(rowIdx, 4).Style.NumberFormat.Format = "#,##0";
                    ws.Cell(rowIdx, 5).Style.NumberFormat.Format = "#,##0";
                    ws.Cell(rowIdx, 6).Style.NumberFormat.Format = "0.0";
                    rowIdx++;
                }

                ws.Columns().AdjustToContents();
                ws.Range(1, 1, rowIdx - 1, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(1, 1, rowIdx - 1, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Eletteljesitmeny_Riport_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
        }
        public async Task<IActionResult> YieldDistributionReport(int? year)
        {
            int rYear = year ?? DateTime.Today.Year;
            var vm = await GetYieldDistributionData(rYear);
            return View(vm);
        }
        [HttpGet]
        public async Task<IActionResult> ExportYieldDistributionToExcel(int year)
        {
            // Itt hívjuk meg a korábban megírt adatgyűjtő logikát
            var vm = await GetYieldDistributionData(year);

            using (var workbook = new XLWorkbook())
            {
                // 1. Munkalap: Első laktáció
                var ws1 = workbook.Worksheets.Add("1. Laktáció");
                WriteDistributionSheet(ws1, "1. Laktációs tehenek - " + year, vm.Months, vm.FirstLactationGroups, null);

                // 2. Munkalap: 2+ laktáció + Összesítő
                var ws2 = workbook.Worksheets.Add("2+ Laktáció");
                WriteDistributionSheet(ws2, "2+ Laktációs tehenek - " + year, vm.Months, vm.MultiLactationGroups, vm.TotalCowsByMonth);

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Savos_Eloszlas_{year}.xlsx");
                }
            }
        }

        // Segédmetódus a táblázat megírásához
        private void WriteDistributionSheet(IXLWorksheet ws, string title, List<string> months, List<YieldBracketGroup> groups, List<int> totals)
        {
            ws.Cell(1, 1).Value = title;
            ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);

            // Fejléc
            ws.Cell(3, 1).Value = "Sáv (kg)";
            for (int i = 0; i < months.Count; i++) ws.Cell(3, i + 2).Value = months[i];
            ws.Range(3, 1, 3, months.Count + 1).Style.Fill.SetBackgroundColor(XLColor.LightGray).Font.SetBold();

            // Adatok
            int rowIdx = 4;
            foreach (var g in groups)
            {
                ws.Cell(rowIdx, 1).Value = g.Bracket;
                for (int i = 0; i < g.MonthlyCounts.Count; i++)
                {
                    // Ha az érték 0, akkor üres stringet írunk, egyébként a számot
                    if (g.MonthlyCounts[i] == 0)
                    {
                        ws.Cell(rowIdx, i + 2).Value = string.Empty;
                    }
                    else
                    {
                        ws.Cell(rowIdx, i + 2).Value = g.MonthlyCounts[i];
                    }
                }
                rowIdx++;
            }

            // Ha van összesítő (csak a második lapon)
            if (totals != null)
            {
                ws.Cell(rowIdx, 1).Value = "Összes fejt tehén:";
                ws.Cell(rowIdx, 1).Style.Font.SetBold();
                for (int i = 0; i < totals.Count; i++) ws.Cell(rowIdx, i + 2).Value = totals[i];
                ws.Range(rowIdx, 1, rowIdx, months.Count + 1).Style.Fill.SetBackgroundColor(XLColor.AliceBlue).Font.SetBold();
            }

            ws.Columns().AdjustToContents();
        }
        private async Task<YieldDistributionViewModel> GetYieldDistributionData(int year)
        {
            var months = Enumerable.Range(1, 12).ToList();
            var allData = await _context.MilkProductions
                .Where(p => p.Date.Year == year)
                .ToListAsync();

            var brackets = new List<string> { "0-10", "11-20", "21-30", "31-40", "41-50", "51-60", "61-70", "71-80" };

            var vm = new YieldDistributionViewModel
            {
                Year = year,
                Months = months.Select(m => System.Globalization.DateTimeFormatInfo.CurrentInfo.GetMonthName(m)).ToList()
            };

            foreach (var bracket in brackets)
            {
                var firstGroup = new YieldBracketGroup { Bracket = bracket, MonthlyCounts = new List<int>() };
                var multiGroup = new YieldBracketGroup { Bracket = bracket, MonthlyCounts = new List<int>() };

                string[] parts = bracket.Split('-');
                int lower = int.Parse(parts[0]);
                int upper = int.Parse(parts[1]);

                foreach (var m in months)
                {
                    var monthlyAverages = allData.Where(p => p.Date.Month == m)
                        .GroupBy(p => new { p.CattleId, p.LactationNo })
                        .Select(g => new {
                            IsFirstLact = g.Key.LactationNo == 1,
                            Avg = g.Average(p => p.TotalYield)
                        }).ToList();

                    firstGroup.MonthlyCounts.Add(monthlyAverages.Count(x => x.IsFirstLact && x.Avg >= lower && x.Avg <= upper));
                    multiGroup.MonthlyCounts.Add(monthlyAverages.Count(x => !x.IsFirstLact && x.Avg >= lower && x.Avg <= upper));
                }
                vm.FirstLactationGroups.Add(firstGroup);
                vm.MultiLactationGroups.Add(multiGroup);
            }

            vm.TotalCowsByMonth = months.Select(m =>
                allData.Where(p => p.Date.Month == m).Select(p => p.CattleId).Distinct().Count()).ToList();

            return vm;
        }

    }
}