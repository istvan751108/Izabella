using ClosedXML.Excel;
using Izabella.Models;
using Izabella.Models.DTOs;
using Izabella.Models.ViewModels;
using Izabella.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace Izabella.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ManureController : Controller
    {
        private readonly ManureCalculationService _calc;
        private readonly IzabellaDbContext _context;
        private readonly VoucherService _voucher;
        private readonly SolidManureService _solidService;

        public ManureController(
            ManureCalculationService calc,
            IzabellaDbContext context,
            VoucherService voucher,
            SolidManureService solidService)
        {
            _calc = calc;
            _context = context;
            _voucher = voucher;
            _solidService = solidService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSolidMulti(DateTime date, [FromBody] List<SolidEntryDto> entries)
        {
            var result = await _solidService.ProcessDailyAsync(date.Date, entries);
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitLiquid([FromBody] LiquidSubmitDto dto)
        {
            if (dto == null)
                return BadRequest("Nincs adat");

            // 1. Számítások elvégzése a szervizzel
            var result = _calc.CalculateLiquid(dto.TotalAmount);

            // 2. Bevételi bizonylat generálása (+)
            var vIn = await _voucher.CreateVoucherAsync("+");
            string formattedVoucherIn = _voucher.FormatVoucher(vIn);

            // 3. Kimeneti bizonylatok generálása (-) és listázása
            var splits = dto.Splits ?? new List<LiquidSplitDto>();
            var voucherOutStrings = new List<string>();

            // Segédlista a későbbi Split rekordokhoz
            var generatedVouchersForSplits = new List<(string VoucherNum, double Amount)>();

            if (!splits.Any())
            {
                // Ha nincs bontás, egyetlen bizonylat készül a teljes összegről
                var vOut = await _voucher.CreateVoucherAsync("-");
                var vNum = _voucher.FormatVoucher(vOut);
                voucherOutStrings.Add(vNum);
                generatedVouchersForSplits.Add((vNum, dto.TotalAmount));
            }
            else
            {
                // Ha van bontás, minden tételhez külön bizonylat készül
                foreach (var s in splits)
                {
                    var vOut = await _voucher.CreateVoucherAsync("-");
                    var vNum = _voucher.FormatVoucher(vOut);
                    voucherOutStrings.Add(vNum);
                    generatedVouchersForSplits.Add((vNum, s.Amount));
                }
            }

            // 4. A fő rekord összeállítása (Itt már minden adat megvan a fejlécbe!)
            var record = new LiquidManure
            {
                Date = dto.Date,
                TotalAmount = dto.TotalAmount,
                Cow = result.Cow,
                Young6_9 = result.Young6_9,
                Young9_12 = result.Young9_12,
                Young12Preg = result.Young12Preg,
                PregnantHeifer = result.PregnantHeifer,
                VoucherIn = formattedVoucherIn,
                VoucherOut = string.Join(", ", voucherOutStrings) // Ez menti el a fejlécbe a vesszővel elválasztott listát
            };

            _context.LiquidManures.Add(record);

            // Elmentjük a fő rekordot, hogy kapjon Id-t
            await _context.SaveChangesAsync();

            // 5. A részletező tábla (Splits) feltöltése
            foreach (var item in generatedVouchersForSplits)
            {
                var splitRecord = new LiquidManureSplit
                {
                    LiquidManureId = record.Id, // Itt használjuk a frissen kapott Id-t
                    Amount = item.Amount,
                    VoucherNumber = item.VoucherNum
                };
                _context.LiquidManureSplits.Add(splitRecord);
            }

            // Végső mentés a spliteknek
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        [HttpPost]
        public async Task<IActionResult> SubmitSolid(double gross, double tare)
        {
            var net = _calc.CalculateNet(gross, tare);
            var result = _calc.CalculateSolid(net);

            // Bizonylat generálás (+1 vagy +2)
            var voucherIn = await _voucher.CreateVoucherAsync("+");
            var voucherOut = await _voucher.CreateVoucherAsync("-");

            var record = new SolidManure
            {
                Date = System.DateTime.Now,
                Gross = gross,
                Tare = tare,
                Net = net,
                Cow = result.Cow,
                CalfMilk = result.CalfMilk,
                Calf3_6 = result.Calf3_6,
                Young6_9 = result.Young6_9,
                Young9_12 = result.Young9_12,
                PregnantHeifer = result.PregnantHeifer,
                VoucherIn = _voucher.FormatVoucher(voucherIn),
                VoucherOut = _voucher.FormatVoucher(voucherOut)
            };
            _context.SolidManures.Add(record);
            await _context.SaveChangesAsync();

            return Json(new { success = true, result, voucherIn = record.VoucherIn, voucherOut = record.VoucherOut });
        }
        // Hígtrágya törlése
        [HttpPost]
        public async Task<IActionResult> DeleteLiquid(int id)
        {
            var record = await _context.LiquidManures
                .Include(l => l.Splits)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (record == null) return BadRequest("Nem található rekord.");

            // 1. Megkeressük a bizonylatokat a Vouchers táblában is a sorszámok alapján
            // Feltételezzük, hogy a VoucherService formátuma: "T YYYY/WH/Sequence"
            // Kinyerjük a sorszámokat a törlendő rekordból
            var vouchersToDelete = new List<string>();
            vouchersToDelete.Add(record.VoucherIn);
            if (record.Splits != null)
            {
                vouchersToDelete.AddRange(record.Splits.Select(s => s.VoucherNumber));
            }

            foreach (var vStr in vouchersToDelete)
            {
                // Szétszedjük a bizonylatszámot, hogy megkeressük a Vouchers táblában
                // Példa: "+ 2024/204/000123" -> kinyerjük belőle a 123-at
                var parts = vStr.Split('/');
                if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
                {
                    var vType = parts[0].Split(' ')[0]; // "+" vagy "-"
                    var vYear = int.Parse(parts[0].Split(' ')[1]);

                    var vEntry = await _context.Vouchers
                        .FirstOrDefaultAsync(v => v.SequenceNumber == seq && v.Type == vType && v.Year == vYear);

                    if (vEntry != null) _context.Vouchers.Remove(vEntry);
                }
            }

            // 2. Töröljük a fő rekordot és a részleteket
            _context.LiquidManureSplits.RemoveRange(record.Splits);
            _context.LiquidManures.Remove(record);

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // Istállótrágya Napi Összesítő Törlése (Bizonylat visszanyeréssel)
        [HttpPost]
        public async Task<IActionResult> DeleteSolidDaily(int id)
        {
            var record = await _context.SolidManureDailies.FirstOrDefaultAsync(x => x.Id == id);
            if (record == null) return BadRequest("Nem található rekord.");

            // 1. Bizonylatok visszanyerése (Voucher tábla takarítása)
            var vouchersToDelete = new List<string> { record.VoucherIn, record.VoucherOut };

            foreach (var vStr in vouchersToDelete.Where(s => !string.IsNullOrEmpty(s)))
            {
                var parts = vStr.Split('/');
                if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
                {
                    var vType = parts[0].Split(' ')[0]; // "+" vagy "-"
                    var vYear = int.Parse(parts[0].Split(' ')[1]);

                    var vEntry = await _context.Vouchers
                        .FirstOrDefaultAsync(v => v.SequenceNumber == seq && v.Type == vType && v.Year == vYear);

                    if (vEntry != null) _context.Vouchers.Remove(vEntry);
                }
            }

            // 2. Kapcsolódó szállítmányok (Loads) törlése
            var loads = _context.SolidManureLoads.Where(l => l.Date.Date == record.Date.Date);
            _context.SolidManureLoads.RemoveRange(loads);

            _context.SolidManureDailies.Remove(record);

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> StornoSolidDaily(int id)
        {
            var originalDaily = await _context.SolidManureDailies.FirstOrDefaultAsync(x => x.Id == id);
            if (originalDaily == null) return Json(new { success = false });

            // ÚJ bizonylatok kérése
            var vIn = await _voucher.CreateVoucherAsync("+");
            var vOut = await _voucher.CreateVoucherAsync("-");

            var stornoDaily = new SolidManureDaily
            {
                Date = originalDaily.Date,
                TotalNet = -originalDaily.TotalNet,
                Cow = -originalDaily.Cow,
                CalfMilk = -originalDaily.CalfMilk,
                Calf3_6 = -originalDaily.Calf3_6,
                Young6_9 = -originalDaily.Young6_9,
                Young9_12 = -originalDaily.Young9_12,
                PregnantHeifer = -originalDaily.PregnantHeifer,
                VoucherIn = "STORNO " + _voucher.FormatVoucher(vIn),
                VoucherOut = "STORNO " + _voucher.FormatVoucher(vOut)
            };
            _context.SolidManureDailies.Add(stornoDaily);

            // Szállítmányok stornózása (itt nem kellenek új bizonylatok, csak a rendszám jelzése)
            var originalLoads = await _context.SolidManureLoads
                .Where(l => l.Date.Date == originalDaily.Date.Date)
                .ToListAsync();

            foreach (var load in originalLoads)
            {
                _context.SolidManureLoads.Add(new SolidManureLoad
                {
                    Date = load.Date,
                    LicensePlate = "STORNO-" + load.LicensePlate,
                    Destination = load.Destination,
                    GrossWeight = -load.GrossWeight,
                    TareWeight = -load.TareWeight,
                    NetWeight = -load.NetWeight
                });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        [HttpPost]
        public async Task<IActionResult> StornoLiquid(int id)
        {
            var original = await _context.LiquidManures.Include(l => l.Splits).FirstOrDefaultAsync(x => x.Id == id);
            if (original == null) return Json(new { success = false, message = "Eredeti rekord nem található." });

            // ÚJ bizonylatok kérése a szerviztől
            var vIn = await _voucher.CreateVoucherAsync("+");
            var vOut = await _voucher.CreateVoucherAsync("-");

            var stornoRecord = new LiquidManure
            {
                Date = DateTime.Now,
                TotalAmount = -original.TotalAmount,
                Cow = -original.Cow,
                Young6_9 = -original.Young6_9,
                Young9_12 = -original.Young9_12,
                Young12Preg = -original.Young12Preg,
                PregnantHeifer = -original.PregnantHeifer,
                // Az új sorszámot mentjük el, de elé írjuk, hogy STORNO
                VoucherIn = "STORNO " + _voucher.FormatVoucher(vIn),
                VoucherOut = "STORNO " + _voucher.FormatVoucher(vOut)
            };

            _context.LiquidManures.Add(stornoRecord);
            await _context.SaveChangesAsync();

            // Splitek (bontások) stornózása új sorszámokkal
            foreach (var split in original.Splits)
            {
                var vSplit = await _voucher.CreateVoucherAsync("-");
                _context.LiquidManureSplits.Add(new LiquidManureSplit
                {
                    LiquidManureId = stornoRecord.Id,
                    Amount = -split.Amount,
                    VoucherNumber = "STORNO " + _voucher.FormatVoucher(vSplit)
                });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        [HttpGet]
        public IActionResult ExportToExcel(int year, int month)
        {
            var reportData = GetReportData(year, month);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Havi Riport");
                // Globális betűméret beállítása (opcionális, de szebb)
                worksheet.Style.Font.FontSize = 10;

                int currentRow = 1;

                // --- 1. SZEKCIÓ: HÍGTRÁGYA ---
                var title1 = worksheet.Cell(currentRow, 1);
                title1.Value = "HÍGTRÁGYA RÉSZLETEZŐ";
                // Egységesen 10 oszlopot vonunk össze mindenhol!
                var range1 = worksheet.Range(currentRow, 1, currentRow, 10);
                ApplyTitleStyle(range1);

                currentRow++;
                string[] headers1 = { "Dátum", "Összes (m³)", "Tehén", "6-9 hó", "9-12 hó", "12-vemhes", "Vemhes", "+ Bizonylat", "- Bizonylat" };
                ApplyHeaderStyle(worksheet, currentRow, headers1);

                foreach (var l in reportData.Liquids.OrderBy(x => x.Date))
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = l.Date.ToString("yyyy.MM.dd");
                    worksheet.Cell(currentRow, 2).Value = l.TotalAmount;
                    worksheet.Cell(currentRow, 3).Value = l.Cow;
                    worksheet.Cell(currentRow, 4).Value = l.Young6_9;
                    worksheet.Cell(currentRow, 5).Value = l.Young9_12;
                    worksheet.Cell(currentRow, 6).Value = l.Young12Preg;
                    worksheet.Cell(currentRow, 7).Value = l.PregnantHeifer;
                    worksheet.Cell(currentRow, 8).Value = l.VoucherIn;

                    var splits = l.Splits.Select(s => $"{s.VoucherNumber} ({s.Amount} m³)");
                    var cell9 = worksheet.Cell(currentRow, 9);
                    cell9.Value = string.Join(Environment.NewLine, splits);
                    cell9.Style.Alignment.WrapText = true;

                    // Keret minden sorra
                    worksheet.Range(currentRow, 1, currentRow, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // --- 2. SZEKCIÓ: SZÁLLÍTMÁNYOK ---
                currentRow += 2;
                var title2 = worksheet.Cell(currentRow, 1);
                title2.Value = "ISTÁLLÓTRÁGYA - SZÁLLÍTMÁNYOK";
                ApplyTitleStyle(worksheet.Range(currentRow, 1, currentRow, 10));

                currentRow++;
                string[] headers2 = { "Dátum", "Rendszám", "Célállomás", "Bruttó", "Tára", "Nettó (kg)" };
                ApplyHeaderStyle(worksheet, currentRow, headers2);

                foreach (var load in reportData.Loads.OrderBy(x => x.Date))
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = load.Date.ToString("yyyy.MM.dd");
                    worksheet.Cell(currentRow, 2).Value = load.LicensePlate;
                    worksheet.Cell(currentRow, 3).Value = load.Destination;
                    worksheet.Cell(currentRow, 4).Value = load.GrossWeight;
                    worksheet.Cell(currentRow, 5).Value = load.TareWeight;
                    worksheet.Cell(currentRow, 6).Value = load.NetWeight;
                    worksheet.Range(currentRow, 1, currentRow, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // --- 3. SZEKCIÓ: NAPI ÖSSZESÍTŐ ---
                currentRow += 2;
                var title3 = worksheet.Cell(currentRow, 1);
                title3.Value = "ISTÁLLÓTRÁGYA - NAPI ÖSSZESÍTÉS";
                ApplyTitleStyle(worksheet.Range(currentRow, 1, currentRow, 10));

                currentRow++;
                string[] headers3 = { "Dátum", "Napi nettó", "Tehén", "Borjú", "3-6 hó", "6-9 hó", "9-12 hó", "Vemhes", "+ Biz", "- Biz" };
                ApplyHeaderStyle(worksheet, currentRow, headers3);

                foreach (var day in reportData.Days.OrderBy(x => x.Date))
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = day.Date.ToString("yyyy.MM.dd");
                    worksheet.Cell(currentRow, 2).Value = day.TotalNet;
                    worksheet.Cell(currentRow, 3).Value = day.Cow;
                    worksheet.Cell(currentRow, 4).Value = day.CalfMilk;
                    worksheet.Cell(currentRow, 5).Value = day.Calf3_6;
                    worksheet.Cell(currentRow, 6).Value = day.Young6_9;
                    worksheet.Cell(currentRow, 7).Value = day.Young9_12;
                    worksheet.Cell(currentRow, 8).Value = day.PregnantHeifer;
                    worksheet.Cell(currentRow, 9).Value = day.VoucherIn;

                    // Itt a 10. oszlop kezelése (VoucherOut vesszővel vagy új sorral)
                    var vOutCell = worksheet.Cell(currentRow, 10);
                    vOutCell.Value = day.VoucherOut?.Replace(", ", Environment.NewLine);
                    vOutCell.Style.Alignment.WrapText = true;

                    worksheet.Range(currentRow, 1, currentRow, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // --- VÉGSŐ FORMÁZÁS: OSZLOPSZÉLESSÉGEK ---
                worksheet.Column(1).Width = 12;  // Dátum
                worksheet.Column(2).Width = 12;  // Összes/Nettó
                worksheet.Column(3).Width = 10;  // Állat kategóriák...
                worksheet.Column(4).Width = 10;
                worksheet.Column(5).Width = 10;
                worksheet.Column(6).Width = 10;
                worksheet.Column(7).Width = 10;
                worksheet.Column(8).Width = 15;  // + Bizonylat
                worksheet.Column(9).Width = 45;  // - Bizonylat (Hígtrágya)
                worksheet.Column(10).Width = 45; // - Biz (Napi összesítő utolsó oszlop)

                // Keretezés javítása (mindenre ami kapott értéket)
                worksheet.Columns().AdjustToContents(); // Először próbáljuk meg az automatát
                                                        // De a bizonylatokat kényszerítsük:
                worksheet.Column(9).Width = 45;
                worksheet.Column(10).Width = 45;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Riport_{year}_{month:D2}.xlsx");
                }
            }
        }

        // Segédfüggvények a kód tisztaságáért:
        private void ApplyTitleStyle(IXLRange range)
        {
            range.Merge().Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.FromHtml("#4F81BD"))
                .Font.SetFontColor(XLColor.White)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        }

        private void ApplyHeaderStyle(IXLWorksheet ws, int row, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(row, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }
        private DailyReportVm GetReportData(int year, int month)
        {
            var model = new DailyReportVm { Year = year, Month = month };

            // 1. Hígtrágya adatok lekérése
            model.Liquids = _context.LiquidManures
                .Include(l => l.Splits)
                .Where(l => l.Date.Year == year && l.Date.Month == month)
                .ToList();

            // 2. Szilárdtrágya szállítmányok lekérése
            model.Loads = _context.SolidManureLoads
                .Where(l => l.Date.Year == year && l.Date.Month == month)
                .ToList();

            // 3. Napi összesítők lekérése
            model.Days = _context.SolidManureDailies
                .Where(d => d.Date.Year == year && d.Date.Month == month)
                .ToList();

            // Összesítők kiszámítása (hogy a fejléc kártyák értékei is stimmeljenek ha kellenek)
            model.LiquidTotal = model.Liquids.Sum(x => x.TotalAmount);
            model.SolidTotal = model.Days.Sum(x => x.TotalNet);

            return model;
        }
    }
}
