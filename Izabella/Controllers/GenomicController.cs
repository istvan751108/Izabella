using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Izabella.Models;

namespace Izabella.Controllers
{
    public class GenomicController : Controller
    {
        private readonly IzabellaDbContext _context;

        public GenomicController(IzabellaDbContext context)
        {
            _context = context;
        }

        // 1. Felület: Váró lista + a kiválasztott hónapban már mintavételezettek
        public async Task<IActionResult> Index(string yearMonth)
        {
            // Ha nincs kiválasztva hónap, az aktuális hónapot vesszük (ÉÉÉÉ-HH formátum)
            if (string.IsNullOrEmpty(yearMonth))
            {
                yearMonth = DateTime.Now.ToString("yyyy-MM");
            }

            var parsedDate = DateTime.ParseExact(yearMonth, "yyyy-MM", null);

            // A: VÁRÓLISTA (Akik aktív üsző borjak és még SOHA nem voltak mintázva)
            ViewBag.Candidates = await _context.Cattles
                .Where(c => c.IsActive
                         && c.AgeGroup == "Itatásos borjú"
                         && c.Gender == Gender.Üsző
                         && c.GenomicTestDate == null)
                .OrderByDescending(c => c.BirthDate)
                .ToListAsync();

            // B: AKTUÁLIS HÓNAPBAN MINTÁZOTTAK (Akiket a választott hónapban mintáztunk meg)
            var testedInMonth = await _context.Cattles
                .Where(c => c.IsActive
                         && c.AgeGroup == "Itatásos borjú"
                         && c.Gender == Gender.Üsző
                         && c.GenomicTestDate.Value.Year == parsedDate.Year
                         && c.GenomicTestDate.Value.Month == parsedDate.Month)
                .OrderByDescending(c => c.GenomicTestDate)
                .ToListAsync();

            ViewBag.SelectedYearMonth = yearMonth;

            return View(testedInMonth);
        }

        // 2. Mintavétel rögzítése a MAI dátummal
        [HttpPost]
        public async Task<IActionResult> RecordSample(int id, string yearMonth)
        {
            var cattle = await _context.Cattles.FindAsync(id);
            if (cattle == null) return NotFound();

            // Elmentjük a pontos mai dátumot a mintavételhez
            cattle.GenomicTestDate = DateTime.Now;
            await _context.SaveChangesAsync();

            // Visszairányítunk, megtartva az aktuálisan nézett hónapot
            return RedirectToAction(nameof(Index), new { yearMonth = yearMonth });
        }

        // 3. EXPORT: Kizárólag a kiválasztott hónap mintáit menti ki!
        public async Task<IActionResult> ExportToExcel(string yearMonth)
        {
            if (string.IsNullOrEmpty(yearMonth))
            {
                yearMonth = DateTime.Now.ToString("yyyy-MM");
            }

            var parsedDate = DateTime.ParseExact(yearMonth, "yyyy-MM", null);

            // CSAK az adott év és hónap mintáit kérjük le
            var testedCattle = await _context.Cattles
                .Where(c => c.IsActive
                         && c.AgeGroup == "Itatásos borjú"
                         && c.Gender == Gender.Üsző
                         && c.GenomicTestDate != null
                         && c.GenomicTestDate.Value.Year == parsedDate.Year
                         && c.GenomicTestDate.Value.Month == parsedDate.Month)
                .OrderBy(c => c.GenomicTestDate)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Genomvizsgálat");

                // Fejlécek
                worksheet.Cell(1, 1).Value = "Fülszám";
                worksheet.Cell(1, 2).Value = "ENAR szám";
                worksheet.Cell(1, 3).Value = "Születési dátum";
                worksheet.Cell(1, 4).Value = "Anya ENAR száma";
                worksheet.Cell(1, 5).Value = "Apa KPLSZ (KLSZ)";
                worksheet.Cell(1, 6).Value = "Iker volt-e";
                worksheet.Cell(1, 7).Value = "Mintavétel dátuma"; // Új oszlop az ellenőrizhetőségért

                worksheet.Row(1).Style.Font.Bold = true;

                int currentRow = 2;
                foreach (var item in testedCattle)
                {
                    worksheet.Cell(currentRow, 1).Value = item.EarTag;
                    worksheet.Cell(currentRow, 2).SetValue(item.EnarNumber);
                    worksheet.Cell(currentRow, 3).Value = item.BirthDate.ToString("yyyy.MM.dd");
                    worksheet.Cell(currentRow, 4).Value = item.MotherEnar ?? "Nincs adat";
                    worksheet.Cell(currentRow, 5).Value = item.FatherKlsz ?? "Nincs adat";
                    worksheet.Cell(currentRow, 6).Value = item.IsTwin ? "Igen" : "Nem";
                    worksheet.Cell(currentRow, 7).Value = item.GenomicTestDate.Value.ToString("yyyy.MM.dd");

                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    var fileName = $"Genom_Export_{yearMonth}.xlsx"; // A fájlnévben is szerepel a hónap

                    return File(content,
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                fileName);
                }
            }
        }
    }
}