using ClosedXML.Excel;
using iText.Forms;
using iText.Kernel.Pdf;
using Izabella.Models;
using Izabella.Models.ViewModels;
using Izabella.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace Izabella.Controllers
{
    public class CattleReportsController : Controller
    {
        private readonly IzabellaDbContext _context;
        private readonly CalvingDbfExportService _dbfExportService;

        public CattleReportsController(IzabellaDbContext context, CalvingDbfExportService dbfExportService)
        {
            _context = context;
            _dbfExportService = dbfExportService;
        }

        // 1. VETÉLÉSI JELENTÉS
        public async Task<IActionResult> AbortionReport(int? year, int? month)
        {
            var targetYear = year ?? DateTime.Now.Year;
            var targetMonth = month ?? DateTime.Now.Month;

            var histories = await _context.AnimalHistories
                .Include(h => h.Cattle)
                .Where(h => h.Type == "Vetélés" &&
                            h.EventDate.Year == targetYear &&
                            h.EventDate.Month == targetMonth)
                .ToListAsync();

            var report = new List<AbortionReportViewModel>();

            foreach (var h in histories)
            {
                int totalAborts = await _context.AnimalHistories
                    .CountAsync(x => x.CattleId == h.CattleId && x.Type == "Vetélés");

                report.Add(new AbortionReportViewModel
                {
                    EarTag = h.Cattle.EarTag,
                    AbortionDate = h.EventDate,
                    AbortionCount = totalAborts,
                    Comment = h.Comment
                });
            }

            ViewBag.CurrentYear = targetYear;
            ViewBag.CurrentMonth = targetMonth;
            return View(report);
        }

        // 2. VÁRHATÓ ELLÉSEK JELENTÉS
        public async Task<IActionResult> ExpectedCalvings()
        {
            var today = DateTime.Today;
            var pregnantCattle = await _context.Cattles
                .Where(c => c.PregnancyStatus == PregnancyStatus.Vemhes && c.LastInseminationDate != null)
                .ToListAsync();

            var report = pregnantCattle.Select(c => new ExpectedCalvingViewModel
            {
                EarTag = c.EarTag,
                LastInseminationDate = c.LastInseminationDate.Value,
                ExpectedDate = c.LastInseminationDate.Value.AddDays(276),
                DaysSinceInsem = (today - c.LastInseminationDate.Value).Days
            })
            .OrderBy(r => r.ExpectedDate)
            .ToList();

            // GRAFIKON ADATOK: Havi bontás yyyy.MM szerint
            var chartData = report
                .GroupBy(r => r.ExpectedDate.ToString("yyyy.MM"))
                .Select(g => new
                {
                    Month = g.Key,
                    Total = g.Count(),
                    Overdue = g.Count(x => x.IsOverdue)
                })
                .OrderBy(g => g.Month)
                .ToList();

            ViewBag.ChartLabels = chartData.Select(x => x.Month).ToList();
            ViewBag.ChartTotals = chartData.Select(x => x.Total).ToList();
            ViewBag.ChartOverdue = chartData.Select(x => x.Overdue).ToList();

            return View(report);
        }

        public async Task<IActionResult> CalvingCharts()
        {
            var today = DateTime.Today;
            var pregnantCattle = await _context.Cattles
                .Where(c => c.PregnancyStatus == PregnancyStatus.Vemhes && c.LastInseminationDate != null)
                .ToListAsync();

            // Adatok csoportosítása hónapok szerint (yyyy.MM kulccsal)
            var monthlyStats = pregnantCattle
                .Select(c => new
                {
                    Expected = c.LastInseminationDate.Value.AddDays(276),
                    IsOverdue = (today - c.LastInseminationDate.Value).Days > 300
                })
                .GroupBy(x => x.Expected.ToString("yyyy.MM"))
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Month = g.Key,
                    Count = g.Count(),
                    OverdueCount = g.Count(x => x.IsOverdue)
                })
                .ToList();

            ViewBag.Labels = monthlyStats.Select(s => s.Month).ToList();
            ViewBag.Counts = monthlyStats.Select(s => s.Count).ToList();
            ViewBag.OverdueCounts = monthlyStats.Select(s => s.OverdueCount).ToList();

            return View();
        }

        public async Task<IActionResult> DownloadSlaughterSupport(int year, int month, int companyId)
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null) return NotFound();

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Vágások lekérése az adott hónapban
            var slaughters = await _context.AnimalHistories
                .Include(h => h.Cattle)
                .Include(h => h.Herd) // Itt tároljuk a tenyészetkódot
                .Where(h => h.Type == "Vágás" && h.EventDate >= startDate && h.EventDate <= endDate)
                .ToListAsync();

            var reportData = slaughters
                .GroupBy(s => s.Comment) // Tegyük fel, hogy a Comment-ben tárolod a Vevőt/Vágóhidat
                .Select(g =>
                {
                    var row = new SlaughterSupportViewModel { DestinationName = g.Key };
                    foreach (var s in g)
                    {
                        // Pontos korszámítás hónapokban
                        int ageInMonths = GetAgeInMonths(s.Cattle.BirthDate, s.EventDate);

                        if (ageInMonths <= 6) row.Count0to6++;
                        else if (ageInMonths < 24) row.Count6to24++;
                        else row.CountOver24++;
                    }
                    return row;
                }).ToList();

            // Itt hívjuk majd meg a QuestPDF generátort
            // byte[] pdfBytes = _pdfService.GenerateSlaughterPdf(reportData, company, startDate);
            // return File(pdfBytes, "application/pdf", $"Vago_Tamogatas_{year}_{month}.pdf");

            return View(reportData); // Ideiglenesen, amíg a PDF kész nem lesz
        }

        private int GetAgeInMonths(DateTime birthDate, DateTime eventDate)
        {
            int months = (eventDate.Year - birthDate.Year) * 12 + eventDate.Month - birthDate.Month;
            if (eventDate.Day < birthDate.Day) months--;
            return months;
        }

        public async Task<IActionResult> GenerateSlaughterPdf(int year, int month, int companyId)
        {
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            var config = await _context.SupportFormConfigs.FirstOrDefaultAsync() ?? new SupportFormConfig();
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Adatok lekérése
            var allData = await _context.AnimalHistories
                .Include(h => h.Cattle)
                .Include(h => h.Customer)
                .Include(h => h.Herd)
                .Where(h => h.Type == "Vágás" && h.EventDate >= startDate && h.EventDate <= endDate)
                .ToListAsync();

            // Csoportosítás: Elsődlegesen TENYÉSZET, másodlagosan VEVŐ szerint
            var groupedByHerd = allData
                .GroupBy(h => h.Herd) // Itt tenyészetenként választjuk szét
                .ToList();

            if (!groupedByHerd.Any()) return NotFound("Nincs vágási adat a megadott időszakban.");

            using (var zipStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var herdGroup in groupedByHerd)
                    {
                        var herd = herdGroup.Key;
                        var herdCode = herd?.HerdCode ?? "Ismeretlen";

                        var tableData = herdGroup
                            .GroupBy(h => h.Customer?.Name)
                            .Select(g => new
                            {
                                CustomerName = g.Key ?? "Ismeretlen",
                                Count0_6 = g.Count(x => GetAgeInMonths(x.Cattle.BirthDate, x.EventDate) <= 6),
                                Count6_24 = g.Count(x =>
                                {
                                    var age = GetAgeInMonths(x.Cattle.BirthDate, x.EventDate);
                                    return age > 6 && age < 24;
                                }),
                                Count24Plus = g.Count(x => GetAgeInMonths(x.Cattle.BirthDate, x.EventDate) >= 24)
                            }).ToList();

                        // Itt hívjuk a PDF generálót
                        byte[] pdfBytes = await CreateSinglePdfBytes(tableData, company, startDate, config, herdCode);

                        // Itt a javított név: startDate:yyyy_MM
                        var entry = archive.CreateEntry($"Tamogatas_{herdCode}_{startDate:yyyy_MM}.pdf");
                        using (var entryStream = entry.Open())
                        {
                            await entryStream.WriteAsync(pdfBytes, 0, pdfBytes.Length);
                        }
                    }
                }

                zipStream.Position = 0;
                return File(zipStream.ToArray(), "application/zip", $"Vago_Tamogatasok_{company.Name}_{year}_{month}.zip");
            }
        }

        private async Task<byte[]> CreateSinglePdfBytes(dynamic groupedData, Company company, DateTime targetMonth, SupportFormConfig config, string herdCode)
        {
            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates", "2334_sablon.pdf");

            using MemoryStream ms = new MemoryStream();
            PdfReader reader = new PdfReader(templatePath);
            PdfWriter writer = new PdfWriter(ms);
            PdfDocument pdfDoc = new PdfDocument(reader, writer);
            PdfAcroForm form = PdfAcroForm.GetAcroForm(pdfDoc, true);
            var fields = form.GetAllFormFields();

            // ALAPADATOK
            if (fields.ContainsKey("DATA_GAZID")) fields["DATA_GAZID"].SetValue(company.ClientId);
            if (fields.ContainsKey("DATA_UNEVE")) fields["DATA_UNEVE"].SetValue(company.Name);
            if (fields.ContainsKey("DATA_A41")) fields["DATA_A41"].SetValue(herdCode);
            if (fields.ContainsKey("DATA_GAZID2")) fields["DATA_GAZID2"].SetValue(config.LicenseeClientId);
            if (fields.ContainsKey("DATA_UNEV2E")) fields["DATA_UNEV2E"].SetValue(config.LicenseeName);
            if (fields.ContainsKey("DATA_1")) fields["DATA_1"].SetValue(targetMonth.Year.ToString());
            if (fields.ContainsKey("DATA_2")) fields["DATA_2"].SetValue(targetMonth.Month.ToString("D2"));
            if (fields.ContainsKey("DATA_ALHEL")) fields["DATA_ALHEL"].SetValue(config.FilingPlace + ",");
            DateTime today = DateTime.Now;
            if (fields.ContainsKey("DATA_ALDAT")) fields["DATA_ALDAT"].SetValue(today.Year.ToString());
            if (fields.ContainsKey("DATA_ALDAT_")) fields["DATA_ALDAT_"].SetValue(today.Month.ToString("D2"));
            if (fields.ContainsKey("DATA_ALDAT__")) fields["DATA_ALDAT__"].SetValue(today.Day.ToString("D2"));

            // Táblázat kitöltése (Ugyanaz a logika, mint eddig)
            int row = 1;
            int s0 = 0, s1 = 0, s2 = 0;
            foreach (var item in groupedData)
            {
                if (row <= 9)
                {
                    fields[$"MARHA_RH_{row}"].SetValue(item.CustomerName);
                    fields[$"MARHA_KCSOP1_{row}"].SetValue(item.Count0_6.ToString());
                    fields[$"MARHA_KCSOP2_{row}"].SetValue(item.Count6_24.ToString());
                    fields[$"MARHA_KCSOP3_{row}"].SetValue(item.Count24Plus.ToString());
                    fields[$"MARHA_OSSZKCS_{row}"].SetValue((item.Count0_6 + item.Count6_24 + item.Count24Plus).ToString());
                    row++;
                }
                s0 += item.Count0_6; s1 += item.Count6_24; s2 += item.Count24Plus;
            }

            // Összesítő
            if (fields.ContainsKey("DATA_OKORCSOP1{$SOR}")) fields["DATA_OKORCSOP1{$SOR}"].SetValue(s0.ToString());
            if (fields.ContainsKey("DATA_OKORCSOP2{$SOR}")) fields["DATA_OKORCSOP2{$SOR}"].SetValue(s1.ToString());
            if (fields.ContainsKey("DATA_OKORCSOP3{$SOR}")) fields["DATA_OKORCSOP3{$SOR}"].SetValue(s2.ToString());
            // 2. OLDAL - GYÓGYSZEREK KITÖLTÉSE (SORSZ1_x: Név, TENEL1_x: Hatóanyag)
            // Első gyógyszer
            if (fields.ContainsKey("SORSZ1_1")) fields["SORSZ1_1"].SetValue(config.Medication1Name);
            if (fields.ContainsKey("TENEL1_1")) fields["TENEL1_1"].SetValue(config.Medication1Agent);

            // Második gyógyszer
            if (fields.ContainsKey("SORSZ1_2")) fields["SORSZ1_2"].SetValue(config.Medication2Name);
            if (fields.ContainsKey("TENEL1_2")) fields["TENEL1_2"].SetValue(config.Medication2Agent);

            // Harmadik gyógyszer
            if (fields.ContainsKey("SORSZ1_3")) fields["SORSZ1_3"].SetValue(config.Medication3Name);
            if (fields.ContainsKey("TENEL1_3")) fields["TENEL1_3"].SetValue(config.Medication3Agent);

            // Ha a többi (4-15) mezőt üresen akarod hagyni vagy nullázni:
            for (int i = 4; i <= 15; i++)
            {
                if (fields.ContainsKey($"SORSZ1_{i}")) fields[$"SORSZ1_{i}"].SetValue("");
                if (fields.ContainsKey($"TENEL1_{i}")) fields[$"TENEL1_{i}"].SetValue("");
            }
            form.FlattenFields();
            pdfDoc.Close();

            return ms.ToArray();
        }

        // 1. A főoldal, ahol a paramétereket megadjuk
        public async Task<IActionResult> SlaughterSupportIndex()
        {
            ViewBag.Companies = await _context.Companies.ToListAsync();
            // Alapértelmezettnek az előző hónapot állítjuk be
            ViewBag.SelectedMonth = DateTime.Now.AddMonths(-1).Month;
            ViewBag.SelectedYear = DateTime.Now.Year;

            return View();
        }

        // 2. A konfiguráció szerkesztése (GET)
        public async Task<IActionResult> EditSupportConfig()
        {
            var config = await _context.SupportFormConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new SupportFormConfig(); // Alapértelmezett értékekkel a modellből
            }
            return View(config);
        }

        // 3. A konfiguráció mentése (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSupportConfig(SupportFormConfig config)
        {
            if (ModelState.IsValid)
            {
                if (config.Id == 0) _context.Add(config);
                else _context.Update(config);

                await _context.SaveChangesAsync();
                TempData["Success"] = "Beállítások elmentve.";
                return RedirectToAction(nameof(SlaughterSupportIndex));
            }
            return View(config);
        }

        public async Task<IActionResult> GeneratePregnantHeiferPdf(int year, int month, int companyId)
        {
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null) return NotFound("A kiválasztott cég nem található.");

            var config = await _context.SupportFormConfigs.FirstOrDefaultAsync() ?? new SupportFormConfig();

            var reportDate = new DateTime(year, month, 1);
            var pregnancyCutoff = DateTime.Now.AddDays(-90);

            // Adatok lekérése: Vemhes, nem tehén, és legalább 90 napja termékenyítve
            var pregnantAnimals = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .Where(c => c.CompanyId == companyId && // <--- EZ A KRITIKUS SOR
                            c.AgeGroup != "Tehén" &&
                            c.PregnancyStatus == PregnancyStatus.Vemhes &&
                            c.LastInseminationDate <= pregnancyCutoff &&
                            c.IsActive)
                .ToListAsync();

            // Ha nincs az adott cégnek vemhes üszője, ne is menjünk tovább
            if (pregnantAnimals == null || !pregnantAnimals.Any())
            {
                // Itt dönthetsz: hibaüzenetet küldesz vissza vagy Redirect-et
                TempData["ErrorMessage"] = $"{company.Name} cégnek nincs a feltételeknek megfelelő vemhes üszője.";
                return RedirectToAction(nameof(PregnantSupportIndex));
            }

            var groupedByHerd = pregnantAnimals.GroupBy(a => a.CurrentHerd).ToList();

            if (!groupedByHerd.Any()) return NotFound("Nincs megfelelő vemhes üsző az adatbázisban.");

            using (var zipStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var herdGroup in groupedByHerd)
                    {
                        var herdCode = herdGroup.Key?.HerdCode ?? "Ismeretlen";
                        var enarList = herdGroup
                            .Select(a => a.EnarNumber.Replace("HU", "").Substring(0, 10))
                            .ToList();

                        // PDF generálása (akár több oldalas)
                        byte[] pdfBytes = await CreatePregnantPdfBytes(enarList, company, reportDate, config, herdCode);

                        var entry = archive.CreateEntry($"VemhesUszo_{herdCode}_{reportDate:yyyy_MM}.pdf");
                        using (var entryStream = entry.Open())
                        {
                            await entryStream.WriteAsync(pdfBytes, 0, pdfBytes.Length);
                        }
                    }
                }
                zipStream.Position = 0;
                return File(zipStream.ToArray(), "application/zip", $"Vemhes_Tamogatasok_{reportDate:yyyy_MM}.zip");
            }
        }

        private async Task<byte[]> CreatePregnantPdfBytes(List<string> enars, Company company, DateTime targetMonth, SupportFormConfig config, string herdCode)
        {
            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates", "2335_sablon.pdf");
            int maxPerPage = 108;
            int pageCount = (int)Math.Ceiling((double)enars.Count / maxPerPage);

            using (MemoryStream outputMs = new MemoryStream())
            {
                PdfWriter writer = new PdfWriter(outputMs);
                PdfDocument resultPdf = new PdfDocument(writer);

                for (int p = 0; p < pageCount; p++)
                {
                    byte[] filledPageBytes;

                    // 1. LÉPÉS: Sablon kitöltése és lementése egy ideiglenes tömbbe
                    using (MemoryStream tempMs = new MemoryStream())
                    {
                        using (PdfReader reader = new PdfReader(templatePath))
                        {
                            PdfDocument sourcePdf = new PdfDocument(reader, new PdfWriter(tempMs));
                            PdfAcroForm form = PdfAcroForm.GetAcroForm(sourcePdf, true);
                            var fields = form.GetAllFormFields();

                            // Mezők kitöltése (Változatlan rész)
                            if (fields.ContainsKey("DATA_GAZID")) fields["DATA_GAZID"].SetValue(company.ClientId);
                            if (fields.ContainsKey("DATA_UNEVE")) fields["DATA_UNEVE"].SetValue(company.Name);
                            if (fields.ContainsKey("DATA_SZEKHELY")) fields["DATA_SZEKHELY"].SetValue(company.Address);
                            if (fields.ContainsKey("DATA_A41")) fields["DATA_A41"].SetValue(herdCode);
                            if (fields.ContainsKey("DATA_NULL")) fields["DATA_NULL"].SetValue(enars.Count.ToString());
                            if (fields.ContainsKey("DATA_1")) fields["DATA_1"].SetValue(targetMonth.Year.ToString());
                            if (fields.ContainsKey("DATA_2")) fields["DATA_2"].SetValue(targetMonth.Month.ToString("D2"));

                            var pageEnars = enars.Skip(p * maxPerPage).Take(maxPerPage).ToList();
                            for (int i = 0; i < pageEnars.Count; i++)
                            {
                                int col = (i % 6) + 1;
                                int row = (i / 6) + 1;
                                string fieldName = $"SZLSZ{col}_{row}";
                                if (fields.ContainsKey(fieldName)) fields[fieldName].SetValue(pageEnars[i]);
                            }

                            // Gyógyszerek és Dátumok kitöltése

                            if (fields.ContainsKey("SORSZ1_1")) fields["SORSZ1_1"].SetValue(config.HeiferMed1Name ?? "");
                            if (fields.ContainsKey("TENEL1_1")) fields["TENEL1_1"].SetValue(config.HeiferMed1Agent ?? "");

                            if (fields.ContainsKey("SORSZ1_2")) fields["SORSZ1_2"].SetValue(config.HeiferMed2Name ?? "");
                            if (fields.ContainsKey("TENEL1_2")) fields["TENEL1_2"].SetValue(config.HeiferMed2Agent ?? "");

                            if (fields.ContainsKey("SORSZ1_3")) fields["SORSZ1_3"].SetValue(config.HeiferMed3Name ?? "");
                            if (fields.ContainsKey("TENEL1_3")) fields["TENEL1_3"].SetValue(config.HeiferMed3Agent ?? "");
                            DateTime today = DateTime.Now;
                            if (fields.ContainsKey("DATA_ALHEL")) fields["DATA_ALHEL"].SetValue(config.FilingPlace + ",");
                            if (fields.ContainsKey("DATA_ALDAT")) fields["DATA_ALDAT"].SetValue(today.Year.ToString());
                            if (fields.ContainsKey("DATA_ALDAT_")) fields["DATA_ALDAT_"].SetValue(today.Month.ToString("D2"));
                            if (fields.ContainsKey("DATA_ALDAT__")) fields["DATA_ALDAT__"].SetValue(today.Day.ToString("D2"));

                            form.FlattenFields();
                            sourcePdf.Close(); // Itt zárjuk le, hogy a tempMs-be belekerüljön minden
                        }
                        filledPageBytes = tempMs.ToArray();
                    }

                    // 2. LÉPÉS: A már kész, lezárt PDF beolvasása és másolása a végső dokumentumba
                    using (MemoryStream readMs = new MemoryStream(filledPageBytes))
                    {
                        using (PdfReader pageReader = new PdfReader(readMs))
                        {
                            PdfDocument pageDoc = new PdfDocument(pageReader);
                            pageDoc.CopyPagesTo(1, pageDoc.GetNumberOfPages(), resultPdf);
                            pageDoc.Close();
                        }
                    }
                }

                resultPdf.Close();
                return outputMs.ToArray();
            }
        }

        public async Task<IActionResult> PregnantSupportIndex()
        {
            ViewBag.Companies = await _context.Companies.ToListAsync();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ExitReport(int? year, int? month, int? companyId)
        {
            int rYear = year ?? DateTime.Now.Year;
            int rMonth = month ?? DateTime.Now.Month;

            // Alap lekérdezés: csak a Tehenek, akik az adott hónapban kerültek ki
            var query = _context.Cattles
                .Include(c => c.Company)
                .Where(c => !c.IsActive &&
                            c.AgeGroup == "Tehén" && // Csak a tehenek!
                            c.ExitDate.HasValue &&
                            c.ExitDate.Value.Year == rYear &&
                            c.ExitDate.Value.Month == rMonth);

            // Ha van kiválasztott cég, szűrünk rá
            if (companyId.HasValue)
            {
                query = query.Where(c => c.CompanyId == companyId);
            }

            var reportData = await query
                .Join(_context.SaleTransactions,
                      c => c.Id,
                      s => s.CattleId,
                      (c, s) => new { Cattle = c, Receipt = s.ReceiptNumber })
                .ToListAsync();

            var vm = new ExitReportVm
            {
                Year = rYear,
                Month = rMonth,
                SelectedCompanyId = companyId,
                Companies = await _context.Companies.OrderBy(c => c.Name).ToListAsync(),
                CompanyGroups = reportData
                    .GroupBy(x => x.Cattle.Company?.Name ?? "Ismeretlen")
                    .Select(g => new CompanyExitGroup
                    {
                        CompanyName = g.Key,
                        ExitedCattle = g.Select(x =>
                        {
                            x.Cattle.PassportNumber = x.Receipt;
                            return x.Cattle;
                        }).OrderBy(c => c.ExitDate).ToList()
                    }).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExitsToExcel(int year, int month, int? companyId)
        {
            // 1. Alap lekérdezés összeállítása (Csak tehenek és az adott időszak)
            var query = _context.Cattles
                .Include(c => c.Company)
                .Where(c => !c.IsActive &&
                            c.AgeGroup == "Tehén" &&
                            c.ExitDate.HasValue &&
                            c.ExitDate.Value.Year == year &&
                            c.ExitDate.Value.Month == month);

            // 2. Szűrés konkrét cégre, ha érkezett ID
            if (companyId.HasValue)
            {
                query = query.Where(c => c.CompanyId == companyId);
            }

            // 3. Adatok lekérése a bizonylatszámmal (SaleTransaction) együtt
            var reportData = await query
                .Join(_context.SaleTransactions,
                      c => c.Id,
                      s => s.CattleId,
                      (c, s) => new { Cattle = c, Receipt = s.ReceiptNumber })
                .ToListAsync();

            // 4. Fájlnév meghatározása
            string companyNamePart = "Osszes_Ceg";
            if (companyId.HasValue && reportData.Any())
            {
                // Kiemeljük az első találat cégnevét a fájlnévhez
                companyNamePart = reportData.First().Cattle.Company?.Name ?? "Ismeretlen_Ceg";
                // Ékezetek és szóközök takarítása a biztonságos fájlnévért
                companyNamePart = string.Concat(companyNamePart.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
            }
            string fileName = $"Kikerules_{companyNamePart}_{year}_{month:D2}.xlsx";

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Kikerülések");
                int currentRow = 1;

                // Csoportosítás (még ha egy cég van is, a struktúra miatt így a legszebb)
                var groups = reportData.GroupBy(x => x.Cattle.Company?.Name ?? "Ismeretlen");

                foreach (var group in groups)
                {
                    // --- SZEKCIÓ CÍM ---
                    var titleRange = worksheet.Range(currentRow, 1, currentRow, 5);
                    titleRange.Merge().Value = $"{year}.{month:D2}. havi Kikerülés - {group.Key} (Tehenek)";
                    titleRange.Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    titleRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#4F81BD"));
                    titleRange.Style.Font.SetFontColor(XLColor.White);
                    titleRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);

                    currentRow++;

                    // --- FEJLÉC ---
                    string[] headers = { "Fülszám", "Enar-szám", "Kikerülés dátuma", "Mozgás típusa", "Bizonylat száma" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cell(currentRow, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }

                    // --- ADATOK ---
                    foreach (var item in group)
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = item.Cattle.EarTag;
                        worksheet.Cell(currentRow, 2).Value = item.Cattle.EnarNumber;
                        worksheet.Cell(currentRow, 3).Value = item.Cattle.ExitDate?.ToString("yyyy.MM.dd");
                        worksheet.Cell(currentRow, 4).Value = TranslateExitType(item.Cattle.ExitType);
                        worksheet.Cell(currentRow, 5).Value = item.Receipt;

                        worksheet.Range(currentRow, 1, currentRow, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }

                    // --- ÖSSZESÍTŐ ---
                    currentRow++;
                    var footerRange = worksheet.Range(currentRow, 1, currentRow, 5);
                    footerRange.Merge().Value = $"Összes kikerült tehén ({group.Key}): {group.Count()} db";
                    footerRange.Style.Font.Bold = true;
                    footerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    footerRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F2F2F2"));
                    footerRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Medium);

                    currentRow += 2; // Térköz a következő esetleges cégcsoport előtt
                }

                worksheet.Columns().AdjustToContents();
                // Az ENAR és a Bizonylat oszlop legyen kicsit szélesebb fixen
                worksheet.Column(2).Width = 18;
                worksheet.Column(5).Width = 22;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }

        // JAVÍTOTT SEGÉDFÜGGVÉNY
        private string TranslateExitType(ExitType? type)
        {
            if (type == null) return "Nincs megadva";

            return type switch
            {
                ExitType.Vágás => "Értékesítés",           // Ez hiányzott vagy más volt az Excelben
                ExitType.Elhullás => "Elhullás",           // Így fog megjelenni a táblázatban
                ExitType.Tulajdonosváltás => "Tulajdonosváltás",
                ExitType.Export => "Export",
                ExitType.Továbbtartás => "Továbbtartás",
                _ => type.ToString()                       // Alapértelmezett, ha új típust adnál hozzá
            };
        }

        [HttpGet]
        public async Task<IActionResult> ReclassificationReport(int? year, int? month, int? companyId)
        {
            int reportYear = year ?? DateTime.Now.Year;
            int reportMonth = month ?? DateTime.Now.Month;

            var query = _context.Cattles
                .Include(c => c.Company)
                .Where(c => c.DamAgeAtCalving == "Vemhes üsző" || c.DamAgeAtCalving == "Üsző") // A Calving metódusod ezt tölti ki
                .Where(c => c.BirthDate.Year == reportYear && c.BirthDate.Month == reportMonth);

            if (companyId.HasValue)
            {
                query = query.Where(c => c.CompanyId == companyId);
            }

            var calves = await query.ToListAsync();

            // Csoportosítás anyák szerint (hogy ikerellésnél ne szerepeljen kétszer az anya az átminősítési listában)
            var reportData = calves
                .GroupBy(c => c.MotherEnar)
                .Select(g => new
                {
                    MotherEnar = g.Key,
                    CalvingDate = g.Min(c => c.BirthDate), // Az ellés napja
                    Company = g.First().Company
                })
                .ToList();

            // ViewModel összeállítása
            var viewModel = new ReclassificationReportVm
            {
                Year = reportYear,
                Month = reportMonth,
                SelectedCompanyId = companyId,
                Companies = await _context.Companies.OrderBy(c => c.Name).ToListAsync(),
                CompanyGroups = reportData
                    .GroupBy(x => x.Company?.Name ?? "Ismeretlen")
                    .Select(cg => new CompanyReclassGroup
                    {
                        CompanyName = cg.Key,
                        Items = cg.Select(i => new ReclassificationItem
                        {
                            // Itt egy kis kiegészítés: meg kell keresnünk az anya fülszámát az ENAR alapján
                            EnarNumber = i.MotherEnar,
                            EarTag = _context.Cattles.FirstOrDefault(m => m.EnarNumber == i.MotherEnar)?.EarTag ?? "N/A",
                            CalvingDate = i.CalvingDate
                        }).ToList()
                    }).ToList()
            };

            return View(viewModel);
        }

        public async Task<IActionResult> ExportReclassificationToExcel(int year, int month, int? companyId)
        {
            // Ugyanaz a lekérdezés, mint fent...
            var calves = await _context.Cattles
                .Include(c => c.Company)
                .Where(c => c.DamAgeAtCalving == "Vemhes üsző" || c.DamAgeAtCalving == "Üsző")
                .Where(c => c.BirthDate.Year == year && c.BirthDate.Month == month)
                .Where(c => companyId == null || c.CompanyId == companyId)
                .ToListAsync();

            var reportData = calves.GroupBy(c => c.MotherEnar)
                .Select(g => new
                {
                    MotherEnar = g.Key,
                    CalvingDate = g.Min(c => c.BirthDate),
                    Company = g.First().Company,
                    EarTag = _context.Cattles.FirstOrDefault(m => m.EnarNumber == g.Key)?.EarTag ?? "N/A"
                }).ToList();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Átminősítés");

                // Cím sor (a mintád alapján)
                var companyName = companyId.HasValue ? reportData.FirstOrDefault()?.Company?.Name : "Összes cég";
                worksheet.Cell(1, 1).Value = $"{year}.{month:D2}. havi átminősítés {companyName}";
                worksheet.Range(1, 1, 1, 4).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                // Fejléc
                worksheet.Cell(2, 1).Value = "Fülszám";
                worksheet.Cell(2, 2).Value = "Enar szám";
                worksheet.Cell(2, 3).Value = "Kikerülés dátuma";
                worksheet.Cell(2, 4).Value = "Kikerülés kódja";
                worksheet.Range(2, 1, 2, 4).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);

                int row = 3;
                foreach (var item in reportData.OrderBy(x => x.CalvingDate))
                {
                    worksheet.Cell(row, 1).Value = item.EarTag;
                    worksheet.Cell(row, 2).Value = item.MotherEnar;
                    worksheet.Cell(row, 3).Value = item.CalvingDate.ToString("yyyy.MM.dd");
                    worksheet.Cell(row, 4).Value = "Átminősítés";
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Atminosites_{year}_{month:D2}.xlsx");
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> MonthlyCowInventory(int? companyId)
        {
            // Ez a riport mindig az AKTUÁLIS állapotot mutatja
            var query = _context.Cattles
                .Include(c => c.Company)
                .Where(c => c.IsActive && c.AgeGroup == "Tehén");

            if (companyId.HasValue)
            {
                query = query.Where(c => c.CompanyId == companyId);
            }

            var cows = await query.ToListAsync();

            var vm = new MonthlyInventoryVm
            {
                Year = DateTime.Now.Year,
                Month = DateTime.Now.Month,
                SelectedCompanyId = companyId,
                Companies = await _context.Companies.OrderBy(c => c.Name).ToListAsync(),
                CompanyGroups = cows
                    .GroupBy(c => c.Company?.Name ?? "Ismeretlen")
                    .Select(g => new CompanyInventoryGroup
                    {
                        CompanyName = g.Key,
                        EarTags = g.Select(c => c.EarTag).OrderBy(t => t).ToList()
                    }).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportInventoryToExcel(int? companyId)
        {
            var query = _context.Cattles
                .Include(c => c.Company)
                .Where(c => c.IsActive && c.AgeGroup == "Tehén");

            if (companyId.HasValue) query = query.Where(c => c.CompanyId == companyId);

            var data = await query.ToListAsync();
            var groups = data.GroupBy(c => c.Company?.Name ?? "Ismeretlen");

            using (var workbook = new XLWorkbook())
            {
                foreach (var group in groups)
                {
                    var ws = workbook.Worksheets.Add(group.Key.Substring(0, Math.Min(group.Key.Length, 30)));
                    int maxCols = 15; // Hány oszlop legyen egymás mellett

                    // Cím
                    var title = ws.Range(1, 1, 1, maxCols);
                    title.Merge().Value = $"{DateTime.Now.Year}.{DateTime.Now.Month:D2} havi tehén {group.Key}";
                    title.Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    title.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    // Fejlécek (Fülszám minden oszlopba)
                    for (int i = 1; i <= maxCols; i++)
                    {
                        ws.Cell(2, i).Value = "Fülszám";
                        ws.Cell(2, i).Style.Font.Bold = true;
                        ws.Cell(2, i).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        ws.Cell(2, i).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    // Fülszámok rácsos feltöltése
                    var tags = group.Select(c => c.EarTag).OrderBy(t => t).ToList();
                    int row = 3;
                    int col = 1;

                    foreach (var tag in tags)
                    {
                        var cell = ws.Cell(row, col);
                        cell.Value = tag;
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        col++;
                        if (col > maxCols)
                        {
                            col = 1;
                            row++;
                        }
                    }

                    // Összesítő sor
                    int lastRow = col == 1 ? row : row + 1;
                    var footer = ws.Range(lastRow, 1, lastRow, maxCols);
                    footer.Merge().Value = $"Összesen: {tags.Count} db";
                    footer.Style.Font.Bold = true;
                    footer.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Columns().AdjustToContents();
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Tehen_Leltar_{DateTime.Now:yyyy_MM}.xlsx");
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> DailyFeedReport(int? year, int? month, int? companyId)
        {
            int rYear = year ?? DateTime.Now.Year;
            int rMonth = month ?? DateTime.Now.Month;
            int days = DateTime.DaysInMonth(rYear, rMonth);

            var stats = await _context.DailyStats
                .Where(s => s.StatDate.Year == rYear && s.StatDate.Month == rMonth &&
                            (!companyId.HasValue || s.CompanyId == companyId))
                .ToListAsync();

            var vm = new FeedReportVm { Year = rYear, Month = rMonth, DaysInMonth = days };

            foreach (var group in vm.AgeGroups)
            {
                vm.RowData[group] = new List<DayStat>();
                for (int d = 1; d <= days; d++)
                {
                    var date = new DateTime(rYear, rMonth, d);
                    // Ha több cég van és nincs szűrés, összegezzük a cégeket az adott napra
                    var dayData = stats.Where(s => s.StatDate == date && s.AgeGroup == group).ToList();

                    vm.RowData[group].Add(new DayStat
                    {
                        Count = dayData.Sum(x => x.Count),
                        TotalWeight = dayData.Sum(x => x.TotalWeight)
                    });
                }
            }
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportDailyFeedReportToExcel(int? year, int? month, int? companyId)
        {
            int rYear = year ?? DateTime.Now.Year;
            int rMonth = month ?? DateTime.Now.Month;
            int days = DateTime.DaysInMonth(rYear, rMonth);

            var stats = await _context.DailyStats
                .Where(s => s.StatDate.Year == rYear && s.StatDate.Month == rMonth &&
                            (!companyId.HasValue || s.CompanyId == companyId))
                .ToListAsync();

            var ageGroups = new List<string> {
                "Itatásos borjú", "Borjú", "Növendék 6-9", "Növendék 9-12",
                "Növendék 12 hó-tól", "Vemhes üsző", "Tehén"
            };

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Napi Riport");

                // Az utolsó adat-oszlop a (napok * 2) + 1. Az összesítő ezután jön:
                int totalDaysColIndex = (days * 2) + 2;

                // 1. Sor: Főcím
                var titleRange = ws.Range(1, 1, 1, totalDaysColIndex);
                titleRange.Merge().Value = $"Napi Korcsoportos Takarmányozási Riport - {rYear}.{rMonth:D2}";
                titleRange.Style.Font.Bold = true;
                titleRange.Style.Font.FontSize = 14;
                titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // 2-3. Sor: Korcsoport fejléc rögzítése
                ws.Cell(2, 1).Value = "Korcsoport";
                var groupHeader = ws.Range(2, 1, 3, 1);
                groupHeader.Merge().Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                groupHeader.Style.Font.Bold = true;
                groupHeader.Style.Fill.BackgroundColor = XLColor.LightGray;

                for (int d = 1; d <= days; d++)
                {
                    int startCol = (d * 2);
                    int endCol = (d * 2) + 1;

                    var dayCell = ws.Range(2, startCol, 2, endCol);
                    dayCell.Merge().Value = $"{d}.";
                    dayCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    dayCell.Style.Font.Bold = true;
                    dayCell.Style.Fill.BackgroundColor = XLColor.LightGray;

                    ws.Cell(3, startCol).Value = "db";
                    ws.Cell(3, endCol).Value = "kg";
                    ws.Cell(3, startCol).Style.Font.FontSize = 9;
                    ws.Cell(3, endCol).Style.Font.FontSize = 9;
                }

                // Összesítő oszlop FEJLÉC - Kifejezetten a legvégére
                var totalHeaderCell = ws.Range(2, totalDaysColIndex, 3, totalDaysColIndex);
                totalHeaderCell.Merge().Value = "Etetési napok";
                totalHeaderCell.Style.Font.Bold = true;
                totalHeaderCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                totalHeaderCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                totalHeaderCell.Style.Alignment.WrapText = true; // Engedjük a tördelést, ha nem férne el
                totalHeaderCell.Style.Fill.BackgroundColor = XLColor.Amber; // Legyen látványosabb különbség

                // Adatok feltöltése
                int currentRow = 4;
                foreach (var group in ageGroups)
                {
                    ws.Cell(currentRow, 1).Value = group;

                    for (int d = 1; d <= days; d++)
                    {
                        var date = new DateTime(rYear, rMonth, d);
                        var dayData = stats.Where(s => s.StatDate == date && s.AgeGroup == group).ToList();

                        int count = dayData.Sum(x => x.Count);
                        double weight = dayData.Sum(x => x.TotalWeight);

                        if (count > 0) ws.Cell(currentRow, (d * 2)).SetValue(count);
                        else ws.Cell(currentRow, (d * 2)).SetValue("-");

                        if (weight > 0)
                        {
                            ws.Cell(currentRow, (d * 2) + 1).SetValue(weight);
                            ws.Cell(currentRow, (d * 2) + 1).Style.NumberFormat.Format = "#,##0";
                        }
                        else ws.Cell(currentRow, (d * 2) + 1).SetValue("-");
                    }

                    // Etetési napok kiszámítása (Adott korcsoport összesített darabszáma a hónapban)
                    int totalFeedingDays = stats.Where(s => s.AgeGroup == group).Sum(x => x.Count);
                    var resCell = ws.Cell(currentRow, totalDaysColIndex);
                    resCell.SetValue(totalFeedingDays);
                    resCell.Style.Font.Bold = true;
                    resCell.Style.NumberFormat.Format = "#,##0";
                    resCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    resCell.Style.Fill.BackgroundColor = XLColor.LightYellow;

                    currentRow++;
                }

                // Keretek
                var tableRange = ws.Range(2, 1, currentRow - 1, totalDaysColIndex);
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                // Formázás
                ws.Columns(1, totalDaysColIndex).AdjustToContents();

                // Ha az utolsó oszlop még mindig túl kicsi, kényszerítsünk rá egy minimum szélességet
                ws.Column(totalDaysColIndex).Width = 15;

                ws.SheetView.FreezeColumns(1);

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Napi_Riport_{rYear}_{rMonth:D2}.xlsx");
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> MonthlyClosing(int? year, int? month, int? companyId)
        {
            int rYear = year ?? DateTime.Now.Year;
            int rMonth = month ?? DateTime.Now.Month;
            DateTime startDate = new DateTime(rYear, rMonth, 1);
            DateTime endDate = startDate.AddMonths(1).AddDays(-1);

            var vm = new MonthlyClosingVm
            {
                Year = rYear,
                Month = rMonth,
                Companies = await _context.Companies.ToListAsync()
            };

            // 1. ÉRTÉKESÍTÉS ÉS ELHULLÁS (SaleTransactions + Cattle Exit adatok)
            // Megjegyzés: Az elhullás is szerepelhet a SaleTransaction-ben 0-ás árral,
            // vagy a Cattle táblában ExitType.Elhullás-sal.
            var sales = await _context.SaleTransactions
                .Include(s => s.Cattle)
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate)
                .ToListAsync();

            foreach (var sale in sales)
            {
                string group = sale.Cattle.AgeGroup;
                int compId = sale.Cattle.CompanyId;
                string type = sale.Type.ToString(); // Vágás, Továbbtartás, Export

                if (!vm.SalesData.ContainsKey(group)) vm.SalesData[group] = new();
                if (!vm.SalesData[group].ContainsKey(compId)) vm.SalesData[group][compId] = new();
                if (!vm.SalesData[group][compId].ContainsKey(type)) vm.SalesData[group][compId][type] = 0;

                vm.SalesData[group][compId][type]++;
            }

            // ELHULLÁS (Kifejezetten az elhullás típusú kikerülések)
            var deaths = await _context.Cattles
                .Where(c => c.ExitDate >= startDate && c.ExitDate <= endDate && c.ExitType == ExitType.Elhullás)
                .ToListAsync();

            foreach (var death in deaths)
            {
                if (!vm.DeathData.ContainsKey(death.AgeGroup)) vm.DeathData[death.AgeGroup] = new();
                if (!vm.DeathData[death.AgeGroup].ContainsKey(death.CompanyId)) vm.DeathData[death.AgeGroup][death.CompanyId] = 0;
                vm.DeathData[death.AgeGroup][death.CompanyId]++;
            }

            // 2. ELLÉSEK ÉS SZAPORULAT
            var newborns = await _context.Cattles
                .Where(c => c.BirthDate >= startDate && c.BirthDate <= endDate)
                .ToListAsync();

            // Szaporulat számlálása (Borjak száma nem szerint)
            foreach (var calf in newborns.Where(c => c.IsAlive))
            {
                string genderKey = calf.Gender.ToString(); // "Bika" vagy "Üsző"
                if (!vm.OffspringData.ContainsKey(genderKey)) vm.OffspringData[genderKey] = 0;
                vm.OffspringData[genderKey]++;
            }

            // Ellési események meghatározása (Anyánként és naponként csoportosítva)
            var calvingEvents = newborns
                .GroupBy(c => new { c.MotherEnar, c.BirthDate.Date })
                .Select(g => new
                {
                    MotherEnar = g.Key.MotherEnar,
                    Date = g.Key.Date,
                    Calves = g.ToList(),
                    IsTwin = g.Count() > 1 || g.Any(c => c.IsTwin),
                    AnyAlive = g.Any(c => c.IsAlive),
                    DamAgeGroup = g.First().DamAgeAtCalving ?? "Tehén"
                });

            foreach (var ev in calvingEvents)
            {
                string damGroup = ev.DamAgeGroup;
                string calvingType = "";

                if (ev.IsTwin)
                {
                    // Ikerellés: Ha legalább egy borjú él, vagy ha ikernek jelölték
                    // (Akkor is ide kerül, ha mindkettő él, vagy csak az egyik)
                    if (ev.AnyAlive)
                    {
                        calvingType = "Iker";
                    }
                    else
                    {
                        // Iker halva születés = 1 db Hullaellés
                        calvingType = "Hullaellés";
                    }
                }
                else
                {
                    // Normál (szimpla) ellés
                    if (ev.AnyAlive)
                    {
                        calvingType = "Sima";
                    }
                    else
                    {
                        // 1 db halva születés = 1 db Hullaellés
                        calvingType = "Hullaellés";
                    }
                }

                if (!vm.CalvingData.ContainsKey(damGroup)) vm.CalvingData[damGroup] = new();
                if (!vm.CalvingData[damGroup].ContainsKey(calvingType)) vm.CalvingData[damGroup][calvingType] = 0;

                vm.CalvingData[damGroup][calvingType]++;
            }

            // 3. ÁLLOMÁNY (Hó végi záró az adott napon aktív állatokból)
            var inventory = await _context.Cattles
                .Where(c => c.IsActive || (c.ExitDate > endDate))
                .ToListAsync();

            foreach (var animal in inventory)
            {
                string invGroup = (animal.AgeGroup == "Tehén") ? "Tehén" : "Növendék";
                if (!vm.InventoryData.ContainsKey(animal.CompanyId)) vm.InventoryData[animal.CompanyId] = new();
                if (!vm.InventoryData[animal.CompanyId].ContainsKey(invGroup)) vm.InventoryData[animal.CompanyId][invGroup] = 0;

                vm.InventoryData[animal.CompanyId][invGroup]++;
            }

            // 4. TERMÉKENYÍTÉS
            var inseminations = await _context.SemenTransactions
                .Where(t => t.Date >= startDate && t.Date <= endDate && t.Type == TransactionType.Insemination)
                .ToListAsync();

            foreach (var ins in inseminations)
            {
                // Megkeressük az állatot, hogy tudjuk a korcsoportját
                var animal = await _context.Cattles.FirstOrDefaultAsync(c => c.EarTag == ins.CattleEarTag);
                if (animal != null)
                {
                    string group = (animal.AgeGroup == "Tehén") ? "Tehén" : "Üsző";
                    if (!vm.InseminationData.ContainsKey(group)) vm.InseminationData[group] = 0;
                    vm.InseminationData[group]++;
                }
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportMonthlyClosingToExcel(int year, int month, int? companyId)
        {
            var vm = await GetMonthlyClosingData(year, month, companyId);
            var allCompanies = await _context.Companies.ToListAsync();
            // Fontos, hogy az Excelhez is tudjuk az összes céget a fejlécek miatt
            //if (vm.Companies == null || !vm.Companies.Any()) vm.Companies = allCompanies;

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Havi Záró");
                ws.Style.Font.FontName = "Arial";
                ws.Style.Font.FontSize = 10;

                // FŐCÍM, látszódik, ha szűrve van, vagy ha összesített
                string companyHeader = companyId.HasValue && vm.Companies.Any()
                    ? $" - {vm.Companies.First().Name}"
                    : " - Összesített jelentés";

                ws.Cell(1, 1).Value = $"{vm.Year}. {vm.Month:D2} havi záró jelentés" + companyHeader;
                ws.Range(1, 1, 1, 5).Merge().Style.Font.SetBold().Font.FontSize = 14;

                int currentRow = 3;

                // --- 1. ÉRTÉKESÍTÉS ---
                ws.Cell(currentRow, 1).Value = "ÉRTÉKESÍTÉS";
                int salesColumns = 5;
                ws.Range(currentRow, 1, currentRow, salesColumns).Merge().Style.Fill.SetBackgroundColor(XLColor.FromHtml("#00B050")).Font.SetBold().Font.FontColor = XLColor.White;
                currentRow++;
                int salesStart = currentRow;
                ws.Cell(currentRow, 1).Value = "Korcsoport";
                ws.Cell(currentRow, 2).Value = "Vágás";
                ws.Cell(currentRow, 3).Value = "Továbbt.";
                ws.Cell(currentRow, 4).Value = "Export";
                ws.Cell(currentRow, 5).Value = "Összesen";
                currentRow++;
                foreach (var group in vm.AgeGroups)
                {
                    ws.Cell(currentRow, 1).Value = group;
                    int v = 0, t = 0, e = 0;
                    foreach (var comp in vm.Companies)
                    {
                        var d = vm.SalesData.GetValueOrDefault(group)?.GetValueOrDefault(comp.Id);
                        v += d?.GetValueOrDefault("Vágás") ?? 0;
                        t += d?.GetValueOrDefault("Továbbtartás") ?? 0;
                        e += d?.GetValueOrDefault("Export") ?? 0;
                    }
                    ws.Cell(currentRow, 2).Value = v;
                    ws.Cell(currentRow, 3).Value = t;
                    ws.Cell(currentRow, 4).Value = e;
                    ws.Cell(currentRow, 5).Value = (v + t + e);
                    ws.Cell(currentRow, 5).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#FFC000"));
                    currentRow++;
                }
                ApplyTableStyles(ws, salesStart, currentRow - 1, 5);

                currentRow += 2;

                // --- 2. ELHULLÁS ---
                ws.Cell(currentRow, 1).Value = "ELHULLÁS";
                ws.Range(currentRow, 1, currentRow, 2).Merge().Style.Fill.SetBackgroundColor(XLColor.FromHtml("#C00000")).Font.SetBold().Font.FontColor = XLColor.White;
                currentRow++;
                int deathStart = currentRow;
                ws.Cell(currentRow, 1).Value = "Korcsoport";
                ws.Cell(currentRow, 2).Value = "db";
                currentRow++;
                foreach (var group in vm.AgeGroups)
                {
                    ws.Cell(currentRow, 1).Value = group;
                    int dSum = 0;
                    foreach (var comp in vm.Companies) dSum += vm.DeathData.GetValueOrDefault(group)?.GetValueOrDefault(comp.Id) ?? 0;
                    ws.Cell(currentRow, 2).Value = dSum;
                    currentRow++;
                }
                ApplyTableStyles(ws, deathStart, currentRow - 1, 2);

                currentRow += 2;

                // --- 3. TULAJDONOSVÁLTÁS (Mindig megjelenik) ---
                ws.Cell(currentRow, 1).Value = "TULAJDONOSVÁLTÁS";
                ws.Range(currentRow, 1, currentRow, 3).Merge().Style.Fill.SetBackgroundColor(XLColor.Amber).Font.SetBold();
                currentRow++;
                int transferStart = currentRow;
                ws.Cell(currentRow, 1).Value = "Irány"; ws.Cell(currentRow, 2).Value = "Mennyiség"; ws.Cell(currentRow, 3).Value = "M.egys.";
                currentRow++;

                if (vm.TransferData != null && vm.TransferData.Any())
                {
                    foreach (var from in vm.TransferData)
                    {
                        var fromName = allCompanies.FirstOrDefault(c => c.Id == from.Key)?.Name ?? "Ismeretlen";
                        foreach (var to in from.Value)
                        {
                            var toName = allCompanies.FirstOrDefault(c => c.Id == to.Key)?.Name ?? "Ismeretlen";
                            ws.Cell(currentRow, 1).Value = $"{fromName} -> {toName}";
                            ws.Cell(currentRow, 2).Value = to.Value;
                            ws.Cell(currentRow, 3).Value = "db";
                            currentRow++;
                        }
                    }
                }
                else
                {
                    ws.Cell(currentRow, 1).Value = "Nincs adat";
                    ws.Cell(currentRow, 2).Value = 0;
                    ws.Cell(currentRow, 3).Value = "db";
                    currentRow++;
                }
                ApplyTableStyles(ws, transferStart, currentRow - 1, 3);

                currentRow += 2;

                // --- 4. ELLÉSEK / SZAPORULAT ---
                ws.Cell(currentRow, 1).Value = "ELLÉSEK ÉS SZAPORULAT";
                ws.Range(currentRow, 1, currentRow, 5).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
                currentRow++;
                int calvingStart = currentRow;
                ws.Cell(currentRow, 1).Value = "Típus"; ws.Cell(currentRow, 2).Value = "Tehén"; ws.Cell(currentRow, 3).Value = "Üsző"; currentRow++;
                foreach (var type in new[] { "Sima", "Iker", "Hullaellés" })
                {
                    ws.Cell(currentRow, 1).Value = type;
                    ws.Cell(currentRow, 2).Value = vm.CalvingData.GetValueOrDefault("Tehén")?.GetValueOrDefault(type) ?? 0;
                    ws.Cell(currentRow, 3).Value = vm.CalvingData.GetValueOrDefault("Üsző")?.GetValueOrDefault(type) ?? 0;
                    currentRow++;
                }
                ApplyTableStyles(ws, calvingStart, currentRow - 1, 3);

                currentRow += 1;
                int offspringStart = currentRow;
                ws.Cell(currentRow, 1).Value = "Szaporulat"; ws.Cell(currentRow, 2).Value = "db";
                ws.Cell(currentRow, 4).Value = "Termékenyítés"; ws.Cell(currentRow, 5).Value = "db"; currentRow++;
                ws.Cell(currentRow, 1).Value = "Üsző"; ws.Cell(currentRow, 2).Value = vm.OffspringData.GetValueOrDefault("Üsző");
                ws.Cell(currentRow, 4).Value = "Tehén"; ws.Cell(currentRow, 5).Value = vm.InseminationData.GetValueOrDefault("Tehén"); currentRow++;
                ws.Cell(currentRow, 1).Value = "Bika"; ws.Cell(currentRow, 2).Value = vm.OffspringData.GetValueOrDefault("Bika");
                ws.Cell(currentRow, 4).Value = "Üsző"; ws.Cell(currentRow, 5).Value = vm.InseminationData.GetValueOrDefault("Üsző"); currentRow++;
                ApplyTableStyles(ws, offspringStart, currentRow - 1, 5);

                currentRow += 2;

                // --- 5. ÁLLOMÁNY ZÁRÓ ---
                ws.Cell(currentRow, 1).Value = "ÁLLATÁLLOMÁNY ZÁRÓ";
                ws.Range(currentRow, 1, currentRow, 2 + vm.Companies.Count).Merge().Style.Fill.SetBackgroundColor(XLColor.FromHtml("#4F81BD")).Font.SetBold().Font.FontColor = XLColor.White;
                currentRow++;
                int invStart = currentRow;
                ws.Cell(currentRow, 1).Value = "Korcsoport";
                for (int i = 0; i < vm.Companies.Count; i++) ws.Cell(currentRow, 2 + i).Value = vm.Companies[i].Name;
                ws.Cell(currentRow, 2 + vm.Companies.Count).Value = "Összesen";
                currentRow++;
                foreach (var ig in new[] { "Tehén", "Növendék" })
                {
                    ws.Cell(currentRow, 1).Value = ig;
                    int rowSum = 0;
                    for (int i = 0; i < vm.Companies.Count; i++)
                    {
                        int val = vm.InventoryData.GetValueOrDefault(vm.Companies[i].Id)?.GetValueOrDefault(ig) ?? 0;
                        ws.Cell(currentRow, 2 + i).Value = val;
                        rowSum += val;
                    }
                    ws.Cell(currentRow, 2 + vm.Companies.Count).Value = rowSum;
                    currentRow++;
                }
                ApplyTableStyles(ws, invStart, currentRow - 1, 2 + vm.Companies.Count);

                ws.Columns().AdjustToContents();
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Havi_Zaro_{year}_{month}.xlsx");
                }
            }
        }

        // Segédfüggvény a keretezéshez
        private void ApplyTableStyles(IXLWorksheet ws, int startRow, int endRow, int lastCol)
        {
            var range = ws.Range(startRow, 1, endRow, lastCol);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Range(startRow, 1, startRow, lastCol).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F2F2F2"));
        }

        private async Task<MonthlyClosingVm> GetMonthlyClosingData(int year, int month, int? companyId)
        {
            DateTime startDate = new DateTime(year, month, 1);
            DateTime endDate = startDate.AddMonths(1).AddDays(-1);

            var vm = new MonthlyClosingVm { Year = year, Month = month };

            // Csak a kiválasztott céget vagy az összeset töltjük be
            vm.Companies = await _context.Companies
                .Where(c => !companyId.HasValue || c.Id == companyId)
                .ToListAsync();

            var companyIds = vm.Companies.Select(c => c.Id).ToList();

            // 1. ÉRTÉKESÍTÉS ÉS TULAJDONOSVÁLTÁS
            // A SaleTransactions-ben a Tulajdonosváltás is benne van SaleType-ként
            var sales = await _context.SaleTransactions
                .Include(s => s.Cattle)
                .Include(s => s.Customer) // Fontos a vevő neve miatt
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate)
                .Where(s => companyIds.Contains(s.Cattle.CompanyId))
                .ToListAsync();

            foreach (var sale in sales)
            {
                // Itt az ExitType-ot nézzük a Cattle-nél a SaleType helyett
                if (sale.Cattle.ExitType == ExitType.Tulajdonosváltás)
                {
                    var targetCompany = await _context.Companies
                        .FirstOrDefaultAsync(c => c.Name == sale.Customer.Name);

                    if (targetCompany != null)
                    {
                        int fromId = sale.Cattle.CompanyId;
                        int toId = targetCompany.Id;

                        if (!vm.TransferData.ContainsKey(fromId)) vm.TransferData[fromId] = new();
                        if (!vm.TransferData[fromId].ContainsKey(toId)) vm.TransferData[fromId][toId] = 0;
                        vm.TransferData[fromId][toId]++;
                    }
                }
                else
                {
                    // Normál eladás
                    string group = sale.Cattle.AgeGroup;
                    int cId = sale.Cattle.CompanyId;
                    string type = sale.Type.ToString(); // SaleType: Vágás, Export stb.

                    if (!vm.SalesData.ContainsKey(group)) vm.SalesData[group] = new();
                    if (!vm.SalesData[group].ContainsKey(cId)) vm.SalesData[group][cId] = new();
                    if (!vm.SalesData[group][cId].ContainsKey(type)) vm.SalesData[group][cId][type] = 0;
                    vm.SalesData[group][cId][type]++;
                }
            }

            // 2. ELHULLÁS
            var deaths = await _context.Cattles
                .Where(c => c.ExitDate >= startDate && c.ExitDate <= endDate && c.ExitType == ExitType.Elhullás)
                .Where(c => companyIds.Contains(c.CompanyId)) // Ezt add hozzá!
                .ToListAsync();

            foreach (var death in deaths)
            {
                if (!vm.DeathData.ContainsKey(death.AgeGroup)) vm.DeathData[death.AgeGroup] = new();
                if (!vm.DeathData[death.AgeGroup].ContainsKey(death.CompanyId)) vm.DeathData[death.AgeGroup][death.CompanyId] = 0;
                vm.DeathData[death.AgeGroup][death.CompanyId]++;
            }

            // 3. ELLÉSEK ÉS SZAPORULAT (A korábban megbeszélt anya-alapú logikával)
            var newborns = await _context.Cattles
                .Where(c => c.BirthDate >= startDate && c.BirthDate <= endDate)
                .ToListAsync();

            foreach (var calf in newborns.Where(c => c.IsAlive))
            {
                string genderKey = calf.Gender.ToString();
                if (!vm.OffspringData.ContainsKey(genderKey)) vm.OffspringData[genderKey] = 0;
                vm.OffspringData[genderKey]++;
            }

            var calvingEvents = newborns
                .GroupBy(c => new { c.MotherEnar, c.BirthDate.Date })
                .Select(g => new
                {
                    IsTwin = g.Count() > 1 || g.Any(c => c.IsTwin),
                    AnyAlive = g.Any(c => c.IsAlive),
                    DamAgeGroup = g.First().DamAgeAtCalving ?? "Tehén"
                });

            foreach (var ev in calvingEvents)
            {
                string damGroup = ev.DamAgeGroup;
                string calvingType = ev.IsTwin ? (ev.AnyAlive ? "Iker" : "Hullaellés") : (ev.AnyAlive ? "Sima" : "Hullaellés");

                if (!vm.CalvingData.ContainsKey(damGroup)) vm.CalvingData[damGroup] = new();
                if (!vm.CalvingData[damGroup].ContainsKey(calvingType)) vm.CalvingData[damGroup][calvingType] = 0;
                vm.CalvingData[damGroup][calvingType]++;
            }

            // 4. ÁLLOMÁNY ZÁRÓ (Szűréssel kiegészítve)
            var inventory = await _context.Cattles
                .Where(c => c.IsActive || (c.ExitDate > endDate))
                .Where(c => companyIds.Contains(c.CompanyId)) // Csak a szűrt cégek állománya
                .ToListAsync();

            foreach (var animal in inventory)
            {
                string invGroup = (animal.AgeGroup == "Tehén") ? "Tehén" : "Növendék";
                if (!vm.InventoryData.ContainsKey(animal.CompanyId)) vm.InventoryData[animal.CompanyId] = new();
                if (!vm.InventoryData[animal.CompanyId].ContainsKey(invGroup)) vm.InventoryData[animal.CompanyId][invGroup] = 0;
                vm.InventoryData[animal.CompanyId][invGroup]++;
            }

            // 5. TERMÉKENYÍTÉS
            var inseminations = await _context.SemenTransactions
                .Where(t => t.Date >= startDate && t.Date <= endDate && t.Type == TransactionType.Insemination)
                .Join(_context.Cattles,
                      ins => ins.CattleEarTag,
                      c => c.EarTag,
                      (ins, c) => new { ins, c.AgeGroup })
                .ToListAsync();

            foreach (var item in inseminations)
            {
                string group = (item.AgeGroup == "Tehén") ? "Tehén" : "Üsző";
                if (!vm.InseminationData.ContainsKey(group)) vm.InseminationData[group] = 0;
                vm.InseminationData[group]++;
            }

            return vm;
        }

        [HttpGet]
        public async Task<IActionResult> MonthlyInventorySummary(int? year, int? month, int? companyId)
        {
            int rYear = year ?? DateTime.Now.Year;
            int rMonth = month ?? DateTime.Now.Month;

            // 1. Lekérjük az adatokat.
            // FONTOS: A GetMonthlyClosingData-ban a vm.Companies-be csak a szűrt cég(ek) kerülnek!
            var vm = await GetMonthlyClosingData(rYear, rMonth, companyId);

            // 2. A szűrőhöz (legördülőhöz) külön lekérjük az ÖSSZES céget
            // Így a szűrőben mindig választható lesz bármelyik cég
            ViewBag.AllCompaniesForFilter = await _context.Companies.OrderBy(c => c.Name).ToListAsync();

            vm.SelectedCompanyId = companyId;

            return View("MonthlyClosing", vm);
        }

        [HttpGet]
        public async Task<IActionResult> OffspringLog(int? year, int? month, int? companyId)
        {
            int rYear = year ?? DateTime.Now.Year;
            int rMonth = month ?? DateTime.Now.Month;

            var vm = await GetOffspringLogData(rYear, rMonth, companyId);
            ViewBag.AllCompaniesForFilter = await _context.Companies.OrderBy(c => c.Name).ToListAsync();

            return View(vm);
        }

        private async Task<OffspringLogVm> GetOffspringLogData(int year, int month, int? companyId)
        {
            DateTime startDate = new DateTime(year, month, 1);
            DateTime endDate = startDate.AddMonths(1).AddDays(-1);

            var vm = new OffspringLogVm { Year = year, Month = month, SelectedCompanyId = companyId };

            // Borjak lekérése az adott időszakban
            var calvesQuery = _context.Cattles
                .Where(c => c.BirthDate >= startDate && c.BirthDate <= endDate)
                .AsQueryable();

            if (companyId.HasValue)
            {
                calvesQuery = calvesQuery.Where(c => c.CompanyId == companyId);
            }

            var calves = await calvesQuery.ToListAsync();

            // Anyák adatainak kikeresése (Fülszám az ENAR alapján)
            var motherEnars = calves.Where(c => !string.IsNullOrEmpty(c.MotherEnar))
                                    .Select(c => c.MotherEnar).Distinct().ToList();

            var mothers = await _context.Cattles
                .Where(c => motherEnars.Contains(c.EnarNumber))
                .ToDictionaryAsync(c => c.EnarNumber, c => c.EarTag);

            foreach (var calf in calves)
            {
                vm.Entries.Add(new OffspringEntry
                {
                    BirthDate = calf.BirthDate,
                    CalfEarTag = calf.EarTag,
                    CalfEnar = calf.EnarNumber,
                    CalfGender = calf.Gender,
                    BirthWeight = calf.BirthWeight,
                    MotherEnar = calf.MotherEnar ?? "Nincs adat",
                    MotherEarTag = (!string.IsNullOrEmpty(calf.MotherEnar) && mothers.ContainsKey(calf.MotherEnar))
                                   ? mothers[calf.MotherEnar]
                                   : "Ismeretlen"
                });
            }

            return vm;
        }

        [HttpGet]
        public async Task<IActionResult> ExportOffspringLogToExcel(int year, int month, int? companyId)
        {
            var vm = await GetOffspringLogData(year, month, companyId);

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Szaporulati Napló");

                // Cím
                ws.Cell(1, 1).Value = $"SZAPORULATI NAPLÓ - {year}. {month:D2}";
                ws.Range(1, 1, 1, 7).Merge().Style.Font.SetBold().Font.FontSize = 14;

                // Fejléc
                string[] headers = { "Születés dátuma", "Anya fülszám", "Anya ENAR", "Borjú fülszám", "Borjú ENAR", "Ivar", "Súly (kg)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(3, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // Adatok
                int row = 4;
                foreach (var entry in vm.Entries.OrderBy(e => e.BirthDate))
                {
                    ws.Cell(row, 1).Value = entry.BirthDate;
                    ws.Cell(row, 2).Value = entry.MotherEarTag;
                    ws.Cell(row, 3).Value = entry.MotherEnar;
                    ws.Cell(row, 4).Value = entry.CalfEarTag;
                    ws.Cell(row, 5).Value = entry.CalfEnar;
                    ws.Cell(row, 6).Value = entry.CalfGender.ToString();
                    ws.Cell(row, 7).Value = entry.BirthWeight;

                    ws.Range(row, 1, row, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    row++;
                }

                ws.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Szaporulati_Naplo_{year}_{month}.xlsx");
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> CalvingIntervalReport(int? year, int? month)
        {
            int selectedYear = year ?? DateTime.Now.Year;
            int selectedMonth = month ?? DateTime.Now.Month;

            // A hónap legelső pillanata: 2026.04.01 00:00:00
            var startDate = new DateTime(selectedYear, selectedMonth, 1);

            // A hónap utolsó napjának legutolsó pillanata: 2026.04.30 23:59:59
            var endDate = startDate.AddMonths(1).AddTicks(-1);

            // Lekérjük az adott hónapban történt ellések history rekordjait (lefedve az egész napot az utolsó ezredmásodpercig)
            var monthlyCalvingHistories = await _context.AnimalHistories
                .Include(h => h.Cattle)
                .Where(h => h.Type == "Ellés" && h.EventDate >= startDate && h.EventDate <= endDate)
                .OrderBy(h => h.EventDate)
                .ToListAsync();

            var multiParousCalvings = monthlyCalvingHistories.Where(h => h.Weight > 0).ToList();
            double averageDays = multiParousCalvings.Any() ? multiParousCalvings.Average(h => h.Weight) : 0;

            var viewModel = new CalvingIntervalReportViewModel
            {
                Year = selectedYear,
                Month = selectedMonth,
                AverageIntervalDays = Math.Round(averageDays, 1),
                TotalCalvingsInMonth = monthlyCalvingHistories.Count,
                MultiParousCalvingsCount = multiParousCalvings.Count
            };

            foreach (var history in monthlyCalvingHistories)
            {
                viewModel.Details.Add(new CalvingIntervalDetailsItem
                {
                    EarTag = history.Cattle?.EarTag ?? "Ismeretlen",
                    EnarNumber = history.Cattle?.EnarNumber ?? "Ismeretlen",
                    CalvingDate = history.EventDate,
                    LactationNo = history.Cattle?.CurrentLactationNo ?? 0,
                    IntervalDays = (int)history.Weight
                });
            }

            ViewBag.Years = Enumerable.Range(DateTime.Now.Year - 5, 6).OrderByDescending(y => y).ToList();
            ViewBag.Months = Enumerable.Range(1, 12).ToList();

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> CalvingBefejesExport()
        {
            // Megkeressük az utolsó olyan history bejegyzést, ami az exportról szólt,
            // hogy lássuk, meddig mentünk el legutóbb
            var utolsoExport = await _context.AnimalHistories
                .Where(h => h.Type == "KÁT_Jelles_Export")
                .OrderByDescending(h => h.EventDate)
                .FirstOrDefaultAsync();

            var model = new CalvingExportSettingsViewModel
            {
                Megye = "14",
                Tenyeszet = "341",
                Telep = "21",
                EnarTeny = "467355",
                BefejesDatuma = DateTime.Now,
                UtolsoBefejesDatuma = utolsoExport?.EventDate ?? DateTime.Now.AddMonths(-1)
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ExportJellesDbf(CalvingExportSettingsViewModel model)
        {
            byte[] fileBytes;

            // 1. LÉPÉS: DBF fájl generálása
            try
            {
                fileBytes = await _dbfExportService.GenerateJellesDbfAsync(
                    model.Megye,
                    model.Tenyeszet,
                    model.Telep,
                    model.BefejesDatuma,
                    model.UtolsoBefejesDatuma
                );
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba történt a DBF fájl generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }

            // 2. LÉPÉS: A határnap elmentése az adatbázisban (Biztonságos, fiktív háttér-rekorddal)
            try
            {
                // Megkeressük a virtuális rendszer-állatunkat az adatbázisban az EarTag alapján
                var rendszerAllat = await _context.Cattles.FirstOrDefaultAsync(c => c.EarTag == "SYSTEM");

                // Ha még soha nem létezett (pl. első futás), létrehozzuk egyszer a modellednek megfelelő kötelező mezőkkel
                if (rendszerAllat == null)
                {
                    rendszerAllat = new Cattle
                    {
                        EarTag = "SYSTEM",
                        EnarNumber = "HU0000000000",
                        PassportNumber = "SYSTEM-EXPORT",
                        PassportSequence = 1,
                        BirthDate = DateTime.Today,
                        Gender = Gender.Üsző,
                        BreedCode = 22,
                        IsActive = false, // Ne zavarjon be az élő állatok listájában
                        IsAlive = true,
                        AgeGroup = "SYSTEM", // 🔥 JAVÍTÁS: Ezzel kiküszöböljük a NOT NULL hibát!
                        CompanyId = 1,      // Írj be egy létező Cég ID-t az adatbázisodból
                        CurrentHerdId = 1   // Írj be een létező Tenyészet ID-t az adatbázisodból
                    };
                    _context.Cattles.Add(rendszerAllat);
                    await _context.SaveChangesAsync();
                }

                var historyLog = new AnimalHistory
                {
                    Type = "KÁT_Jelles_Export",
                    // 🔥 Az EventDate mezőbe mentjük a kiválasztott befejezési dátumot
                    EventDate = model.BefejesDatuma,
                    CattleId = rendszerAllat.Id, // Örökre ehhez a fix virtuális rekordhoz kötjük!
                    Comment = $"Sikeres export. Határnap: {model.BefejesDatuma:yyyy.MM.dd}",
                    IsEnarReported = true
                };

                _context.AnimalHistories.Add(historyLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                ModelState.AddModelError("", "A DBF elkészült, de a dátum mentése sikertelen: " + innerMessage);
                return View("CalvingBefejesExport", model);
            }

            // 3. LÉPÉS: Fájl letöltése a böngészőben
            return File(fileBytes, "application/x-dbf", "jelles.dbf");
        }

        [HttpPost]
        public async Task<IActionResult> ExportTehenKiesesDbf(CalvingExportSettingsViewModel model)
        {
            try
            {
                byte[] fileBytes = await _dbfExportService.GenerateTehenKiesesDbfAsync(
                    model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                );
                return File(fileBytes, "application/x-dbf", "jkieses_tehen.dbf");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba a tehén kiesés generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportUszoKiesesDbf(CalvingExportSettingsViewModel model)
        {
            try
            {
                byte[] fileBytes = await _dbfExportService.GenerateUszoKiesesDbfAsync(
                    model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                );
                // A letöltési név itt is jkieses.dbf legyen, de a teszt kedvéért elnevezheted másnak is
                return File(fileBytes, "application/x-dbf", "jkieses_uszo.dbf");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba az üsző kiesés generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        [HttpPost]
        public IActionResult ExportJtelepDbf(CalvingExportSettingsViewModel model)
        {
            try
            {
                byte[] fileBytes = _dbfExportService.GenerateJtelepDbf(
                    model.Megye, model.Tenyeszet, model.Telep, model.EnarTeny, model.BefejesDatuma
                );
                return File(fileBytes, "application/x-dbf", "jtelep.dbf");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba a jtelep generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportTehenTermekenyitesDbf(CalvingExportSettingsViewModel model)
        {
            try
            {
                byte[] fileBytes = await _dbfExportService.GenerateTehenTermekenyitesDbfAsync(
                    model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                );
                return File(fileBytes, "application/x-dbf", "jterm_tehen.dbf");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba a tehén termékenyítés generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportUszoTermekenyitesDbf(CalvingExportSettingsViewModel model)
        {
            try
            {
                byte[] fileBytes = await _dbfExportService.GenerateUszoTermekenyitesDbfAsync(
                    model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                );
                return File(fileBytes, "application/x-dbf", "jterm_uszo.dbf");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba az üsző termékenyítés generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        [HttpPost]
        public IActionResult ExportUszoUjFelvDbf(CalvingExportSettingsViewModel model)
        {
            try
            {
                byte[] fileBytes = _dbfExportService.GenerateUszoUjFelvEtelDbf();
                return File(fileBytes, "application/x-dbf", "jujfelv_uszo.dbf");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba az üsző új felvétel generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportTehenUjFelvDbf(CalvingExportSettingsViewModel model)
        {
            try
            {
                byte[] fileBytes = await _dbfExportService.GenerateTehenUjFelvetelDbfAsync(
                    model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                );
                return File(fileBytes, "application/x-dbf", "jujfelv_tehen.dbf");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba a tehén új felvétel generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportTehenVemhDbf(CalvingExportSettingsViewModel model)
        {
            try
            {
                byte[] fileBytes = await _dbfExportService.GenerateTehenVemhessegDbfAsync(
                    model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                );
                return File(fileBytes, "application/x-dbf", "jvemh_tehen.dbf");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba a tehén vemhesség generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportUszoVmhDbf(CalvingExportSettingsViewModel model)
        {
            try
            {
                byte[] fileBytes = await _dbfExportService.GenerateUszoVemhessegDbfAsync(
                    model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                );
                return File(fileBytes, "application/x-dbf", "jvemh_uszo.dbf");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba az üsző vemhesség generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        [HttpPost]
        public IActionResult ExportJhibakDbf(CalvingExportSettingsViewModel model)
        {
            try
            {
                byte[] fileBytes = _dbfExportService.GenerateJhibakDbf();
                return File(fileBytes, "application/x-dbf", "jhibak.dbf");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba a hibajegyzék generálása közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportFullKatPackage(CalvingExportSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("CalvingBefejesExport", model);
            }

            try
            {
                using (var zipMS = new MemoryStream())
                {
                    using (var archive = new ZipArchive(zipMS, ZipArchiveMode.Create, true))
                    {
                        // ==========================================
                        // 1. TEHÉN MAPPA TARTALMA (TEHEN/)
                        // ==========================================

                        // jelles.dbf
                        var jellesBytes = await _dbfExportService.GenerateJellesDbfAsync(
                            model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                        );
                        CreateZipEntry(archive, "TEHEN/jelles.dbf", jellesBytes);

                        // jkieses.dbf (Tehén)
                        var jkiesesTehenBytes = await _dbfExportService.GenerateTehenKiesesDbfAsync(
                            model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                        );
                        CreateZipEntry(archive, "TEHEN/jkieses.dbf", jkiesesTehenBytes);

                        // jtelep.dbf (Tehén)
                        var jtelepTehenBytes = _dbfExportService.GenerateJtelepDbf(
                            model.Megye,
                            model.Tenyeszet,
                            model.Telep,
                            model.EnarTeny, // <-- EZT ADTUK HOZZÁ
                            model.BefejesDatuma
                        );
                        CreateZipEntry(archive, "TEHEN/jtelep.dbf", jtelepTehenBytes);

                        // jterm.dbf (Tehén)
                        var jtermTehenBytes = await _dbfExportService.GenerateTehenTermekenyitesDbfAsync(
                            model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                        );
                        CreateZipEntry(archive, "TEHEN/jterm.dbf", jtermTehenBytes);

                        // jujfelv.dbf (Tehén)
                        var jujfelvTehenBytes = await _dbfExportService.GenerateTehenUjFelvetelDbfAsync(
                            model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                        );
                        CreateZipEntry(archive, "TEHEN/jujfelv.dbf", jujfelvTehenBytes);

                        // jvemh.dbf (Tehén)
                        var jvemhTehenBytes = await _dbfExportService.GenerateTehenVemhessegDbfAsync(
                            model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                        );
                        CreateZipEntry(archive, "TEHEN/jvemh.dbf", jvemhTehenBytes);

                        // jhibak.dbf (Üres hibajegyzék a tehenekhez)
                        var jhibakBytes = _dbfExportService.GenerateJhibakDbf();
                        CreateZipEntry(archive, "TEHEN/jhibak.dbf", jhibakBytes);

                        // ==========================================
                        // 2. ÜSZŐ MAPPA TARTALMA (USZO/)
                        // ==========================================

                        // jkieses.dbf (Üsző)
                        var jkiesesUszoBytes = await _dbfExportService.GenerateUszoKiesesDbfAsync(
                            model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                        );
                        CreateZipEntry(archive, "USZO/jkieses.dbf", jkiesesUszoBytes);

                        // jtelep.dbf (Üsző)
                        var jtelepUszoBytes = _dbfExportService.GenerateJtelepDbf(
                            model.Megye,
                            model.Tenyeszet,
                            model.Telep,
                            model.EnarTeny, // <-- EZT ADTUK HOZZÁ
                            model.BefejesDatuma
                        );
                        CreateZipEntry(archive, "USZO/jtelep.dbf", jtelepUszoBytes);

                        // jterm.dbf (Üsző)
                        var jtermUszoBytes = await _dbfExportService.GenerateUszoTermekenyitesDbfAsync(
                            model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                        );
                        CreateZipEntry(archive, "USZO/jterm.dbf", jtermUszoBytes);

                        // jujfelv_uszo.dbf -> A mappában fixen 'jujfelv.dbf' néven fut az üszőknél is!
                        var jujfelvUszoBytes = _dbfExportService.GenerateUszoUjFelvEtelDbf();
                        CreateZipEntry(archive, "USZO/jujfelv.dbf", jujfelvUszoBytes);

                        // jvemh.dbf (Üsző)
                        var jvemhUszoBytes = await _dbfExportService.GenerateUszoVemhessegDbfAsync(
                            model.Megye, model.Tenyeszet, model.Telep, model.BefejesDatuma, model.UtolsoBefejesDatuma
                        );
                        CreateZipEntry(archive, "USZO/jvemh.dbf", jvemhUszoBytes);
                    }

                    // Fájlnév generálása a befejezés hónapja alapján (pl. KAT_befejezes_2026_05.zip)
                    string zipFileName = $"KAT_befejezes_{model.BefejesDatuma:yyyy_MM}.zip";

                    return File(zipMS.ToArray(), "application/zip", zipFileName);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hiba történt a teljes KÁT csomag összekészítése közben: " + ex.Message);
                return View("CalvingBefejesExport", model);
            }
        }

        // Kisegítő metódus a zip bejegyzések tisztább létrehozásához
        private void CreateZipEntry(ZipArchive archive, string entryPath, byte[] content)
        {
            var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
            using (var entryStream = entry.Open())
            {
                entryStream.Write(content, 0, content.Length);
            }
        }
    }
}