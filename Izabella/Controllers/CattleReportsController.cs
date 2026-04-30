using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using iText.Forms;
using iText.Kernel.Pdf;
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
                .Select(g => new {
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
                .Select(c => new {
                    Expected = c.LastInseminationDate.Value.AddDays(276),
                    IsOverdue = (today - c.LastInseminationDate.Value).Days > 300
                })
                .GroupBy(x => x.Expected.ToString("yyyy.MM"))
                .OrderBy(g => g.Key)
                .Select(g => new {
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
                .Select(g => {
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
                            .Select(g => new {
                                CustomerName = g.Key ?? "Ismeretlen",
                                Count0_6 = g.Count(x => GetAgeInMonths(x.Cattle.BirthDate, x.EventDate) <= 6),
                                Count6_24 = g.Count(x => {
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
    }
}