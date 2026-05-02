using ClosedXML.Excel;
using iText.Forms;
using iText.Kernel.Pdf;
using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace Izabella.Controllers
{
    public class CattleReportsController : Controller
    {
        private readonly IzabellaDbContext _context;

        public CattleReportsController(IzabellaDbContext context)
        {
            _context = context;
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
                        ExitedCattle = g.Select(x => {
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
    }
}