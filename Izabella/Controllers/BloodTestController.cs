using Izabella.Models;
using Izabella.Models.ViewModels; // Ha ide tetted a LivestockUnitReport-ot korábban
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel; // FONTOS: Ez kell a ClosedXML-hez

namespace Izabella.Controllers
{
    public class BloodTestController : Controller
    {
        private readonly IzabellaDbContext _context;

        public BloodTestController(IzabellaDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> BloodTestSelector(string ageGroup)
        {
            var cattle = await _context.Cattles
                .Where(c => c.IsActive && (string.IsNullOrEmpty(ageGroup) || c.AgeGroup == ageGroup))
                .OrderBy(c => c.EarTag)
                .ToListAsync();

            ViewBag.AgeGroups = await _context.Cattles
                .Where(c => c.IsActive)
                .Select(c => c.AgeGroup)
                .Distinct()
                .ToListAsync();

            ViewBag.SelectedAgeGroup = ageGroup;
            return View(cattle);
        }

        [HttpPost]
        public async Task<IActionResult> SaveBloodTestList(int[] selectedCattleIds)
        {
            if (selectedCattleIds == null || selectedCattleIds.Length == 0)
            {
                TempData["Error"] = "Nincs kijelölve egyetlen állat sem!";
                return RedirectToAction(nameof(BloodTestSelector));
            }

            // Csak a kijelölteket dolgozzuk fel a beküldött sorrendben
            for (int i = 0; i < selectedCattleIds.Length; i++)
            {
                var animal = await _context.Cattles.FindAsync(selectedCattleIds[i]);
                if (animal != null)
                {
                    animal.BloodTestTubeNumber = i + 1;
                    animal.LastBloodTestDate = DateTime.Now;
                    _context.Update(animal);
                }
            }
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ExportBloodTestExcel), new { ids = selectedCattleIds });
        }

        public IActionResult ExportBloodTestExcel(int[] ids)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Vérvizsgálat_Labor");
                worksheet.Cell(1, 1).Value = "Fülszám";
                worksheet.Cell(1, 2).Value = "Vércső szám";
                worksheet.Row(1).Style.Font.Bold = true;

                var animals = _context.Cattles
                    .Where(c => ids.Contains(c.Id))
                    .OrderBy(c => c.BloodTestTubeNumber).ToList();

                for (int i = 0; i < animals.Count; i++)
                {
                    worksheet.Cell(i + 2, 1).Value = animals[i].EarTag;
                    worksheet.Cell(i + 2, 2).Value = animals[i].BloodTestTubeNumber;
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Labor_Minta_Lista.xlsx");
                }
            }
        }

        public IActionResult ExportBreedingExcel(int[] ids)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Tenyésztési_Adatok");
                string[] headers = { "Fülszám", "ENAR szám", "Születési idő", "Termékenyítés dátuma", "Bika KLSZ" };

                for (int h = 0; h < headers.Length; h++)
                {
                    worksheet.Cell(1, h + 1).Value = headers[h];
                }
                worksheet.Row(1).Style.Font.Bold = true;

                var animals = _context.Cattles.Where(c => ids.Contains(c.Id)).ToList();

                for (int i = 0; i < animals.Count; i++)
                {
                    worksheet.Cell(i + 2, 1).Value = animals[i].EarTag;
                    worksheet.Cell(i + 2, 2).Value = animals[i].EnarNumber;
                    worksheet.Cell(i + 2, 3).Value = animals[i].BirthDate.ToString("yyyy.MM.dd");
                    worksheet.Cell(i + 2, 4).Value = animals[i].LastInseminationDate?.ToString("yyyy.MM.dd") ?? "-";
                    worksheet.Cell(i + 2, 5).Value = animals[i].InseminationBullKlsz ?? "-";
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Tenyész_Adatok_Export.xlsx");
                }
            }
        }
    }
}