using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Services
{
    public class MilkAnalysisService
    {
        private readonly IzabellaDbContext _context;

        public MilkAnalysisService(IzabellaDbContext context)
        {
            _context = context;
        }

        public async Task<CompleteAnalysisReport> GenerateReportDataAsync(int year, int? month, int companyId)
        {
            var company = await _context.Companies.FindAsync(companyId);
            string companyName = company?.Name ?? "Ismeretlen Cég";
            string period = month.HasValue ? $"{year}.{month:D2}" : $"{year}. teljes év";

            // 1. Laboradatok lekérése a kiválasztott időszakra
            var query = _context.MilkLabResults
                .Include(m => m.Cattle)
                .Where(m => m.Cattle.CompanyId == companyId && m.BefDat.Year == year);

            if (month.HasValue)
            {
                query = query.Where(m => m.BefDat.Month == month.Value);
            }

            var labData = await query.AsNoTracking().ToListAsync();

            var report = new CompleteAnalysisReport
            {
                CompanyName = companyName,
                PeriodText = period
            };

            if (!labData.Any()) return report;

            // 2. Szükséges kiegészítő adatok beolvasása a háttérszámításokhoz
            var targetCattleIds = labData.Select(l => l.CattleId).Distinct().ToList();
            var labDates = labData.Select(l => l.BefDat.Date).Distinct().ToList();

            // Napi termelési adatok az érintett napokra
            var productionData = await _context.MilkProductions
                .Where(p => targetCattleIds.Contains(p.CattleId) && labDates.Contains(p.Date.Date))
                .AsNoTracking()
                .ToListAsync();

            var prodLookup = productionData.ToLookup(p => $"{p.CattleId}_{p.Date:yyyyMMdd}");

            // Előző próbafejések kereséséhez lefejjük az összes történelmi laboradatot ezekre az állatokra, időrendben
            var allHistoricalLabs = await _context.MilkLabResults
                .Where(m => targetCattleIds.Contains(m.CattleId))
                .OrderBy(m => m.BefDat)
                .AsNoTracking()
                .ToListAsync();

            // Csoportosítás állatok szerint a gyors memóriabeli kereséshez
            var historicalLabLookup = allHistoricalLabs.ToLookup(m => m.CattleId);

            // 3. Adatok dúsítása valós számításokkal
            var enrichedData = labData.Select(l =>
            {
                string key = $"{l.CattleId}_{l.BefDat:yyyyMMdd}";
                var match = prodLookup[key].FirstOrDefault();

                // --- Laktációs sorszám fallback kezelése ---
                double fallbackLactation = 1.0;
                if (match == null && l.Cattle != null)
                {
                    double totalDaysAlive = (l.BefDat - l.Cattle.BirthDate).TotalDays;
                    if (totalDaysAlive > 1100) fallbackLactation = 2.0;
                    if (totalDaysAlive > 1500) fallbackLactation = 3.0;
                }
                double calvingCount = match != null ? (double)match.LactationNo : fallbackLactation;

                // --- 1. pr.f ssz. (Próbafejés sorszáma a laktációban) KISZÁMÍTÁSA ---
                // Megszámoljuk, hogy az adott állat laktációjában hanyadik próbafejés ez (időrendben legfeljebb a mai napig)
                var animalHistoryLabs = historicalLabLookup[l.CattleId].ToList();

                // Kiszűrjük azokat, amelyek az aktuális laktációhoz tartoznak (ha elérhető a laktációs szám a produkcióban, 
                // akkor az azonos laktációs számúak közül hanyadik, egyébként az utolsó 305 napon belüliek közül)
                double testMilkCount = 1.0;
                if (match != null)
                {
                    // Ha a MilkProduction-ben van laktációs számunk, akkor az adott laktáción belüli sorszámot keressük
                    var currentLactationLabs = animalHistoryLabs
                        .Where(h => h.BefDat <= l.BefDat)
                        .ToList();

                    // Mivel a labor eredményekben magában nincs benne a laktációs szám, a DaysInMilk (Tejelő nap) és a dátumok alapján 
                    // megnézzük, hány próbafejezése volt az állatnak a tejelő napok kezdete óta
                    double daysInMilk = match.DaysInMilk;
                    DateTime lactationStartDate = l.BefDat.AddDays(-daysInMilk);

                    testMilkCount = currentLactationLabs.Count(h => h.BefDat >= lactationStartDate && h.BefDat <= l.BefDat);
                }
                else
                {
                    // Fallback ha nincs produkciós egyezés: az elmúlt 305 napon belüli laborok száma
                    testMilkCount = animalHistoryLabs.Count(h => h.BefDat <= l.BefDat && h.BefDat >= l.BefDat.AddDays(-305));
                }
                if (testMilkCount == 0) testMilkCount = 1.0;

                // --- 2. DEVIATION (Eltérés az előző próbafejéshez képest) KISZÁMÍTÁSA ---
                double deviation = 0.0;
                var previousLab = animalHistoryLabs
                    .Where(h => h.BefDat < l.BefDat)
                    .LastOrDefault(); // Időrendben a közvetlenül megelőző

                if (previousLab != null)
                {
                    deviation = l.NapiTej - previousLab.NapiTej;
                }

                // --- 3. KONDÍCIÓPONT (BCS) BEKÖTÉSE ---
                // Kivesszük az állat modellből az Erzsike által rögzített éles értéket
                double conditionScore = l.Cattle != null ? l.Cattle.BodyConditionScore : 3.50;
                if (conditionScore == 0) conditionScore = 3.50; // Biztonsági mentőöv, ha valamiért 0 maradt volna

                return new
                {
                    Lab = l,
                    DaysInMilk = match != null ? match.DaysInMilk : 100,
                    CalvingCount = calvingCount,
                    TestMilkCount = testMilkCount,
                    Deviation = deviation,
                    ConditionScore = conditionScore
                };
            }).ToList();

            // --- A) TEJELŐ NAP SZERINTI CSOPORTOSÍTÁS (11 kategória) ---
            var dayIntervals = new (string Name, Func<int, bool> Filter)[]
            {
                ("1. 0-30", d => d >= 0 && d <= 30),
                ("2. 31-60", d => d >= 31 && d <= 60),
                ("3. 61-90", d => d >= 61 && d <= 90),
                ("4. 91-120", d => d >= 91 && d <= 120),
                ("5. 121-150", d => d >= 121 && d <= 150),
                ("6. 151-180", d => d >= 151 && d <= 180),
                ("7. 181-210", d => d >= 181 && d <= 210),
                ("8. 211-240", d => d >= 211 && d <= 240),
                ("9. 241-270", d => d >= 241 && d <= 270),
                ("10. 271-305", d => d >= 271 && d <= 305),
                ("11. 306->", d => d > 305)
            };

            foreach (var interval in dayIntervals)
            {
                var subset = enrichedData.Where(x => interval.Filter(x.DaysInMilk)).ToList();
                if (subset.Any())
                {
                    var row = CalculateEnrichedRow(interval.Name, subset);
                    report.LactationDayRows.Add(row);
                }
            }

            // --- B) TEJ KG KATEGÓRIA SZERINTI CSOPORTOSÍTÁS (11 kategória) ---
            var kgIntervals = new (string Name, Func<double, bool> Filter)[]
            {
                ("1. 0-5", k => k >= 0 && k <= 5),
                ("2. 5-10", k => k > 5 && k <= 10),
                ("3. 10-15", k => k > 10 && k <= 15),
                ("4. 15-20", k => k > 15 && k <= 20),
                ("5. 20-25", k => k > 20 && k <= 25),
                ("6. 25-30", k => k > 25 && k <= 30),
                ("7. 30-35", k => k > 30 && k <= 35),
                ("8. 35-40", k => k > 35 && k <= 40),
                ("9. 40-45", k => k > 40 && k <= 45),
                ("10. 45-50", k => k > 45 && k <= 50),
                ("11. 50->", k => k > 50)
            };

            foreach (var interval in kgIntervals)
            {
                var subset = enrichedData.Where(x => interval.Filter(x.Lab.NapiTej)).ToList();
                if (subset.Any())
                {
                    var row = CalculateEnrichedRow(interval.Name, subset);
                    report.MilkVolumeRows.Add(row);
                }
            }

            return report;
        }

        private AnalysisRow CalculateEnrichedRow(string categoryName, IEnumerable<dynamic> subset)
        {
            var labs = subset.Select(x => (MilkLabResult)x.Lab).ToList();

            double avgSugar = labs.Average(l => new[] { l.Cukor1, l.Cukor2, l.Cukor3, l.Cukor4, l.Cukor5, l.Cukor6 }.FirstOrDefault(c => c > 0));
            if (double.IsNaN(avgSugar) || avgSugar == 0) avgSugar = 4.85;

            return new AnalysisRow
            {
                CategoryName = categoryName,
                Count = labs.Count,
                AverageDaysInMilk = subset.Average(x => (double)x.DaysInMilk),

                // Élesített oszlopok átlagai:
                AverageCalvingCount = subset.Average(x => (double)x.CalvingCount),
                AverageTestMilkCount = subset.Average(x => (double)x.TestMilkCount),
                AverageMilkDeviation = subset.Average(x => (double)x.Deviation),
                AverageConditionScore = subset.Average(x => (double)x.ConditionScore),

                AverageMilkKg = labs.Average(l => l.NapiTej),
                AverageFatPercentage = labs.Average(l => l.NapiZsir),
                AverageProteinPercentage = labs.Average(l => l.NapiFeherje),
                AverageSugarPercentage = avgSugar,
                AverageSomaticCell = labs.Average(l => l.SzomatikusSejtszam),
                AverageUrea = labs.Average(l => l.Karbamid)
            };
        }

        public byte[] ExportToExcelBytes(CompleteAnalysisReport report)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws1 = workbook.Worksheets.Add("Tejelő nap összefüggés");
                BuildSheet(ws1, "Tejelő nap", report.LactationDayRows, report, isLactationDaySheet: true);

                var ws2 = workbook.Worksheets.Add("Tej kg kategória");
                BuildSheet(ws2, "Tej kg kat.", report.MilkVolumeRows, report, isLactationDaySheet: false);

                using (var ms = new MemoryStream())
                {
                    workbook.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        private void BuildSheet(IXLWorksheet ws, string mainTitle, List<AnalysisRow> rows, CompleteAnalysisReport report, bool isLactationDaySheet)
        {
            // Főcímek
            ws.Cell(1, 1).Value = $"{report.CompanyName} - {mainTitle} Próbafejés Adatösszefüggés";
            ws.Cell(1, 1).Style.Font.SetBold();
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            ws.Cell(2, 1).Value = $"Időszak: {report.PeriodText}";
            ws.Cell(2, 1).Style.Font.SetItalic();

            // FEJLÉC ÉPÍTÉSE
            int col = 1;
            ws.Cell(4, col++).Value = mainTitle;
            ws.Cell(4, col++).Value = "Tehén (db)";
            ws.Cell(4, col++).Value = "Ell. ssz.";
            ws.Cell(4, col++).Value = "pr.f ssz.";

            if (!isLactationDaySheet)
            {
                ws.Cell(4, col++).Value = "Tejelő nap";
            }

            ws.Cell(4, col++).Value = "Átl. Tej (kg)";
            ws.Cell(4, col++).Value = "Zsír %";
            ws.Cell(4, col++).Value = "Fehérje %";
            ws.Cell(4, col++).Value = "Tejcukor %";
            ws.Cell(4, col++).Value = "Szomatika szám";
            ws.Cell(4, col++).Value = "Karbamid";
            ws.Cell(4, col++).Value = "Eltérés";
            ws.Cell(4, col++).Value = "Kondipont";

            int endCol = col - 1;
            var headerRange = ws.Range(4, 1, 4, endCol);

            headerRange.Style.Font.SetBold();
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#1F497D"));
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // ADATOK KIÍRÁSA
            int rIdx = 5;
            foreach (var row in rows)
            {
                col = 1;
                ws.Cell(rIdx, col++).Value = row.CategoryName;
                ws.Cell(rIdx, col++).Value = row.Count;
                ws.Cell(rIdx, col).Value = row.AverageCalvingCount; ws.Cell(rIdx, col++).Style.NumberFormat.Format = "0.0";
                ws.Cell(rIdx, col).Value = row.AverageTestMilkCount; ws.Cell(rIdx, col++).Style.NumberFormat.Format = "0.0";

                if (!isLactationDaySheet)
                {
                    ws.Cell(rIdx, col).Value = row.AverageDaysInMilk;
                    ws.Cell(rIdx, col++).Style.NumberFormat.Format = "0";
                }

                ws.Cell(rIdx, col).Value = row.AverageMilkKg; ws.Cell(rIdx, col++).Style.NumberFormat.Format = "0.00";
                ws.Cell(rIdx, col).Value = row.AverageFatPercentage / 100.0; ws.Cell(rIdx, col++).Style.NumberFormat.Format = "0.00%";
                ws.Cell(rIdx, col).Value = row.AverageProteinPercentage / 100.0; ws.Cell(rIdx, col++).Style.NumberFormat.Format = "0.00%";
                ws.Cell(rIdx, col).Value = row.AverageSugarPercentage / 100.0; ws.Cell(rIdx, col++).Style.NumberFormat.Format = "0.00%";
                ws.Cell(rIdx, col).Value = row.AverageSomaticCell; ws.Cell(rIdx, col++).Style.NumberFormat.Format = "#,##0";
                ws.Cell(rIdx, col).Value = row.AverageUrea; ws.Cell(rIdx, col++).Style.NumberFormat.Format = "0.0";

                // Eltérés előjelezése és kondipont kivezetése
                ws.Cell(rIdx, col).Value = row.AverageMilkDeviation; ws.Cell(rIdx, col++).Style.NumberFormat.Format = "+0.00;-0.00;0.00";
                ws.Cell(rIdx, col).Value = row.AverageConditionScore; ws.Cell(rIdx, col++).Style.NumberFormat.Format = "0.00";

                ws.Range(rIdx, 1, rIdx, endCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(rIdx, 1, rIdx, endCol).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                rIdx++;
            }

            // ÖSSZESEN / ÁTLAG SOR
            if (rows != null && rows.Any())
            {
                col = 1;
                ws.Cell(rIdx, col++).Value = "Összesen / Átlag";
                ws.Cell(rIdx, col++).Value = rows.Sum(r => r.Count);
                ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageCalvingCount);
                ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageTestMilkCount);

                if (!isLactationDaySheet)
                {
                    ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageDaysInMilk);
                }

                ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageMilkKg);
                ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageFatPercentage) / 100.0;
                ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageProteinPercentage) / 100.0;
                ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageSugarPercentage) / 100.0;
                ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageSomaticCell);
                ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageUrea);
                ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageMilkDeviation);
                ws.Cell(rIdx, col++).Value = rows.Average(r => r.AverageConditionScore);

                var totalRange = ws.Range(rIdx, 1, rIdx, endCol);
                totalRange.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);

                int startFormatCol = isLactationDaySheet ? 5 : 6;
                ws.Cell(rIdx, startFormatCol).Style.NumberFormat.Format = "0.00";
                ws.Cell(rIdx, startFormatCol + 1).Style.NumberFormat.Format = "0.00%";
                ws.Cell(rIdx, startFormatCol + 2).Style.NumberFormat.Format = "0.00%";
                ws.Cell(rIdx, startFormatCol + 3).Style.NumberFormat.Format = "0.00%";
                ws.Cell(rIdx, startFormatCol + 4).Style.NumberFormat.Format = "#,##0";
                ws.Cell(rIdx, startFormatCol + 5).Style.NumberFormat.Format = "0.0";
                ws.Cell(rIdx, startFormatCol + 6).Style.NumberFormat.Format = "+0.00;-0.00;0.00";
                ws.Cell(rIdx, startFormatCol + 7).Style.NumberFormat.Format = "0.00";
            }

            ws.Columns().AdjustToContents();
        }
    }
}