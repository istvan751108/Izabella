using ClosedXML.Excel;
using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Izabella.Controllers
{
    public class MilkReportController : Controller
    {
        private readonly IzabellaDbContext _context;
        private const double KgToLiterFactor = 0.971;

        public MilkReportController(IzabellaDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> MonthlyStatement(string monthFilter, double? customFeedMilk)
        {
            DateTime targetMonth = DateTime.Today;
            if (!string.IsNullOrEmpty(monthFilter) && DateTime.TryParse(monthFilter + "-01", out DateTime parsedDate))
            {
                targetMonth = parsedDate;
            }

            double feedMilkDefault = customFeedMilk ?? 1500;
            ViewBag.CurrentMonthFilter = targetMonth.ToString("yyyy-MM");
            ViewBag.FeedMilk = feedMilkDefault;

            var companies = await _context.Companies.OrderBy(c => c.Name).ToListAsync();
            var reportRows = await GenerateMonthlyReportRows(targetMonth, feedMilkDefault);

            ViewBag.Companies = companies;
            return View(reportRows);
        }

        private async Task<List<MilkReportRow>> GenerateMonthlyReportRows(DateTime targetMonth, double feedMilk)
        {
            int daysInMonth = DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month);

            var sales = await _context.MilkSales
                .Where(s => s.DeliveryDate.Month == targetMonth.Month && s.DeliveryDate.Year == targetMonth.Year)
                .ToListAsync();

            var labs = await _context.MilkQualityLabs
                .Where(l => l.RecordDate.Month == targetMonth.Month && l.RecordDate.Year == targetMonth.Year)
                .ToListAsync();

            // ÚJ: Céges felosztások lekérése az adott hónapra
            var distributions = await _context.MilkCompanyDistributions
                .Where(d => d.DistributionDate.Month == targetMonth.Month && d.DistributionDate.Year == targetMonth.Year)
                .ToListAsync();

            var rows = new List<MilkReportRow>();

            for (int day = 1; day <= daysInMonth; day++)
            {
                var currentDate = new DateTime(targetMonth.Year, targetMonth.Month, day);
                var daySales = sales.Where(s => s.DeliveryDate.Date == currentDate.Date).ToList();

                int dekad = currentDate.Day <= 10 ? 1 : (currentDate.Day <= 20 ? 2 : 3);
                var dayLab = labs.FirstOrDefault(l => l.DekadNumber == dekad)
                             ?? labs.OrderByDescending(l => l.RecordDate).FirstOrDefault();

                double totalKg = daySales.Sum(s => s.WeightKg);

                // ÚJ: Kiválogatjuk az adott naphoz tartozó céges leosztásokat
                var dayDistributions = distributions.Where(d => d.DistributionDate.Date == currentDate.Date).ToList();
                var companyValues = dayDistributions.ToDictionary(
                    d => d.CompanyId,
                    d => (Kg: d.DistributedKg, Liter: d.DistributedLiter)
                );

                rows.Add(new MilkReportRow
                {
                    Date = currentDate,
                    Dekad = dekad,
                    TotalKg = totalKg,
                    TotalLiter = totalKg * KgToLiterFactor,
                    FeedMilkLiter = feedMilk,
                    FatPercentage = dayLab?.FatPercentage ?? 0,
                    ProteinPercentage = dayLab?.ProteinPercentage ?? 0,

                    // JAVÍTVA: Mivel stringek, így üres szöveget adunk vissza null esetén
                    SomaticCellCount = dayLab?.SomaticCellCount ?? "",
                    BacteriaCount = dayLab?.BacteriaCount ?? "",

                    CompanyValues = companyValues
                });
            }

            return rows;
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string monthFilter, double feedMilk)
        {
            DateTime targetMonth = string.IsNullOrEmpty(monthFilter) ? DateTime.Today : DateTime.Parse(monthFilter + "-01");
            var rows = await GenerateMonthlyReportRows(targetMonth, feedMilk);
            var companies = await _context.Companies.OrderBy(c => c.Name).ToListAsync();

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Tej értékesítés");

                // Főcím és alapértelmezett oszlopszélességek rögzítése
                ws.Cell(1, 1).Value = "Dátum";
                ws.Cell(1, 2).Value = "Összes tej (Kg)";
                ws.Cell(1, 3).Value = "Összes tej (Liter)";
                ws.Cell(1, 4).Value = "Takarmánytej (Liter)";

                // Dinamikus cégoszlopok felépítése a fejlécben
                int colIndex = 5;
                foreach (var company in companies)
                {
                    ws.Cell(1, colIndex).Value = $"{company.Name} (Kg)";
                    ws.Cell(1, colIndex + 1).Value = $"{company.Name} (Liter)";
                    colIndex += 2;
                }

                // Labor oszlopok a végére
                ws.Cell(1, colIndex).Value = "Zsír %";
                ws.Cell(1, colIndex + 1).Value = "Fehérje %";
                ws.Cell(1, colIndex + 2).Value = "Szomatika";
                ws.Cell(1, colIndex + 3).Value = "Csíraszám";

                // Fejléc formázása (Félkövér, Szürke háttér, Középre igazítás)
                var headerRange = ws.Range(1, 1, 1, colIndex + 3);
                headerRange.Style.Font.SetBold();
                headerRange.Style.Fill.SetBackgroundColor(XLColor.LightGray);
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                // Adatsorok feltöltése
                int rowIndex = 2;
                foreach (var row in rows)
                {
                    // Dátum formázása szövegesen
                    ws.Cell(rowIndex, 1).Value = row.Date.ToString("yyyy.MM.dd.");

                    // Alapadatok beírása 3 tizedesre kerekítve/formázva
                    ws.Cell(rowIndex, 2).Value = row.TotalKg;
                    ws.Cell(rowIndex, 2).Style.NumberFormat.Format = "0.000";

                    ws.Cell(rowIndex, 3).Value = row.TotalLiter;
                    ws.Cell(rowIndex, 3).Style.NumberFormat.Format = "0.000";

                    ws.Cell(rowIndex, 4).Value = row.FeedMilkLiter;
                    ws.Cell(rowIndex, 4).Style.NumberFormat.Format = "0.000";

                    // Céges felosztások beírása
                    int currentCompanyCol = 5;
                    foreach (var company in companies)
                    {
                        if (row.CompanyValues.TryGetValue(company.Id, out var vals))
                        {
                            // Ha van érték, számként adjuk át, és rákényszerítjük a 3 tizedesjegy formátumot
                            if (vals.Kg > 0)
                            {
                                ws.Cell(rowIndex, currentCompanyCol).Value = vals.Kg;
                                ws.Cell(rowIndex, currentCompanyCol).Style.NumberFormat.Format = "0.000";
                            }
                            if (vals.Liter > 0)
                            {
                                ws.Cell(rowIndex, currentCompanyCol + 1).Value = vals.Liter;
                                ws.Cell(rowIndex, currentCompanyCol + 1).Style.NumberFormat.Format = "0.000";
                            }
                        }
                        currentCompanyCol += 2;
                    }

                    // Labor adatok kezelése - osztva 100-zal a helyes Excel százalék formátum miatt
                    if (row.FatPercentage > 0)
                    {
                        ws.Cell(rowIndex, currentCompanyCol).Value = row.FatPercentage / 100.0;
                        ws.Cell(rowIndex, currentCompanyCol).Style.NumberFormat.Format = "0.00%";
                    }
                    if (row.ProteinPercentage > 0)
                    {
                        ws.Cell(rowIndex, currentCompanyCol + 1).Value = row.ProteinPercentage / 100.0;
                        ws.Cell(rowIndex, currentCompanyCol + 1).Style.NumberFormat.Format = "0.00%";
                    }

                    // Szomatika és Csíraszám rögzítése explicit stringként a SetValue<string> használatával
                    string somaticValue = !string.IsNullOrEmpty(row.SomaticCellCount) ? row.SomaticCellCount : "";
                    ws.Cell(rowIndex, currentCompanyCol + 2).Value = !string.IsNullOrEmpty(row.SomaticCellCount) ? row.SomaticCellCount : "";

                    string bacteriaValue = !string.IsNullOrEmpty(row.BacteriaCount) ? row.BacteriaCount : "";
                    ws.Cell(rowIndex, currentCompanyCol + 3).Value = !string.IsNullOrEmpty(row.BacteriaCount) ? row.BacteriaCount : "";

                    // Rácsvonalak az adatsor köré
                    var rowRange = ws.Range(rowIndex, 1, rowIndex, currentCompanyCol + 3);
                    rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    rowRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    rowIndex++;
                }

                // Oszlopszélességek automatikus igazítása a tartalomhoz
                ws.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    string fileName = $"Tej_Kimutatas_{targetMonth:yyyy_MM}.xlsx";

                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }

        // --- ÚJ: Napi laktációs adatexport indító oldala ---
        [HttpGet]
        public async Task<IActionResult> DailyLactationExport()
        {
            // Alapértelmezett dátum: Megkeressük a legutolsó befejezés dátumát az adatbázisból (ha van ilyen naplózva),
            // vagy ha nincs, a mai napból indulunk ki, és annak vesszük az előző napját.
            DateTime defaultDate = DateTime.Today.AddDays(-1);

            // Ha van a rendszerben korábbi lezárás, lekérheted a legfrissebbet (pl. CalvingLogs vagy egy beállítás táblából)
            // Feltételezve, hogy a mai naphoz képest a tegnapi nap a legbiztonságosabb alapértelmezett:
            var viewModel = new DailyMilkExportViewModel
            {
                TargetDate = defaultDate
            };

            return View(viewModel);
        }

        // --- ÚJ: Excel generálás és letöltés ClosedXML-lel ---
        [HttpPost]
        public async Task<IActionResult> ExportDailyMilkToExcel(DailyMilkExportViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("DailyLactationExport", model);
            }

            // Adatok kigyűjtése a választott napra
            var milkProductions = await _context.MilkProductions
                .Include(m => m.Cattle)
                .Where(m => m.Date.Date == model.TargetDate.Date)
                .OrderBy(m => m.Cattle.EarTag)
                .ToListAsync();

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = workbook.Worksheets.Add($"Tej {model.TargetDate:yyyy-MM-dd}");

                // Fejléc oszlopai
                string[] headers = new string[]
                {
            "Fülszám",
            "ENAR-szám",
            "Istálló / Box",
            "Laktáció",
            "Laktációs nap",
            "1. Műszak (kg)",
            "2. Műszak (kg)",
            "3. Műszak (kg)",
            "Összes tej (kg)",
            "Zsír 1 (%)", "Zsír 2 (%)", "Zsír 3 (%)",
            "Fehérje 1 (%)", "Fehérje 2 (%)", "Fehérje 3 (%)",
            "Becsült adat?",
            "Megjegyzés"
                };

                // Fejléc kiírása
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                }

                // Fejléc formázása (Félkövér, Világoskék háttér, Középre igazítás)
                var headerRange = ws.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.SetBold();
                headerRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F1"));
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                int rowIndex = 2;
                foreach (var prod in milkProductions)
                {
                    // Törzsadatok az állatról
                    ws.Cell(rowIndex, 1).Value = prod.Cattle?.EarTag ?? "";
                    ws.Cell(rowIndex, 2).Value = prod.Cattle?.EnarNumber ?? "";
                    ws.Cell(rowIndex, 3).Value = prod.Cattle?.Stall ?? "-";
                    ws.Cell(rowIndex, 4).Value = prod.LactationNo;
                    ws.Cell(rowIndex, 5).Value = prod.DaysInMilk;

                    // Műszak tejhozamok (számként formázva, 2 tizedesjegy)
                    ws.Cell(rowIndex, 6).Value = prod.Yield1;
                    ws.Cell(rowIndex, 6).Style.NumberFormat.Format = "0.00";

                    ws.Cell(rowIndex, 7).Value = prod.Yield2;
                    ws.Cell(rowIndex, 7).Style.NumberFormat.Format = "0.00";

                    ws.Cell(rowIndex, 8).Value = prod.Yield3;
                    ws.Cell(rowIndex, 8).Style.NumberFormat.Format = "0.00";

                    ws.Cell(rowIndex, 9).Value = prod.TotalYield;
                    ws.Cell(rowIndex, 9).Style.NumberFormat.Format = "0.00";
                    ws.Cell(rowIndex, 9).Style.Font.SetBold(); // Az összesen legyen kiemelve

                    // Beltartalmak (Zsír % - Excel százalék formátumhoz osztva 100-zal, ha nem null)
                    if (prod.Fat1.HasValue) { ws.Cell(rowIndex, 10).Value = prod.Fat1.Value / 100.0; ws.Cell(rowIndex, 10).Style.NumberFormat.Format = "0.00%"; }
                    if (prod.Fat2.HasValue) { ws.Cell(rowIndex, 11).Value = prod.Fat2.Value / 100.0; ws.Cell(rowIndex, 11).Style.NumberFormat.Format = "0.00%"; }
                    if (prod.Fat3.HasValue) { ws.Cell(rowIndex, 12).Value = prod.Fat3.Value / 100.0; ws.Cell(rowIndex, 12).Style.NumberFormat.Format = "0.00%"; }

                    // Beltartalmak (Fehérje %)
                    if (prod.Protein1.HasValue) { ws.Cell(rowIndex, 13).Value = prod.Protein1.Value / 100.0; ws.Cell(rowIndex, 13).Style.NumberFormat.Format = "0.00%"; }
                    if (prod.Protein2.HasValue) { ws.Cell(rowIndex, 14).Value = prod.Protein2.Value / 100.0; ws.Cell(rowIndex, 14).Style.NumberFormat.Format = "0.00%"; }
                    if (prod.Protein3.HasValue) { ws.Cell(rowIndex, 15).Value = prod.Protein3.Value / 100.0; ws.Cell(rowIndex, 15).Style.NumberFormat.Format = "0.00%"; }

                    // Becsült adat jelző vizuálisan (ha igaz, sárga háttérrel jelezzük a sor végén)
                    if (prod.IsEstimated)
                    {
                        ws.Cell(rowIndex, 16).Value = "IGAZ";
                        ws.Cell(rowIndex, 16).Style.Fill.SetBackgroundColor(XLColor.LightYellow);
                    }
                    else
                    {
                        ws.Cell(rowIndex, 16).Value = "NEM";
                    }

                    ws.Cell(rowIndex, 17).Value = prod.Comment ?? "";

                    // Rácsvonalak rögzítése a sorra
                    var rowRange = ws.Range(rowIndex, 1, rowIndex, headers.Length);
                    rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    rowRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    rowIndex++;
                }

                // Automatikus oszlopszélesség
                ws.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    string fileName = $"Afimilk_Korrigalt_Tejadat_{model.TargetDate:yyyy_MM_dd}.xlsx";

                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }
    }

    public class MilkReportRow
    {
        public DateTime Date { get; set; }
        public int Dekad { get; set; }
        public double TotalKg { get; set; }
        public double TotalLiter { get; set; }
        public double FeedMilkLiter { get; set; }
        public double FatPercentage { get; set; }
        public double ProteinPercentage { get; set; }
        public string SomaticCellCount { get; set; }
        public string BacteriaCount { get; set; }

        // ÚJ: Szótár a cégek azonosítója alapján tárolt Kg és Liter értékeknek
        public Dictionary<int, (double Kg, double Liter)> CompanyValues { get; set; } = new Dictionary<int, (double Kg, double Liter)>();
    }
}