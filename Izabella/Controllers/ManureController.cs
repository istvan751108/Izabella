using Izabella.Models;
using Izabella.Models.DTOs;
using Izabella.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace Izabella.Controllers
{
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

            var result = _calc.CalculateLiquid(dto.TotalAmount);

            var voucherIn = await _voucher.CreateVoucherAsync("+");

            var record = new LiquidManure
            {
                Date = dto.Date,
                TotalAmount = dto.TotalAmount,
                Cow = result.Cow,
                Young6_9 = result.Young6_9,
                Young9_12 = result.Young9_12,
                Young12Preg = result.Young12Preg,
                PregnantHeifer = result.PregnantHeifer,
                VoucherIn = _voucher.FormatVoucher(voucherIn)
            };

            _context.LiquidManures.Add(record);
            await _context.SaveChangesAsync();

            var splits = dto.Splits ?? new List<LiquidSplitDto>();

            var voucherList = new List<string>();

            foreach (var s in splits)
            {
                var v = await _voucher.CreateVoucherAsync("-");

                var split = new LiquidManureSplit
                {
                    LiquidManureId = record.Id,
                    Amount = s.Amount,
                    VoucherNumber = _voucher.FormatVoucher(v)
                };

                voucherList.Add(split.VoucherNumber);
                _context.LiquidManureSplits.Add(split);
            }

            if (!splits.Any())
            {
                var v = await _voucher.CreateVoucherAsync("-");

                var split = new LiquidManureSplit
                {
                    LiquidManureId = record.Id,
                    Amount = dto.TotalAmount,
                    VoucherNumber = _voucher.FormatVoucher(v)
                };

                voucherList.Add(split.VoucherNumber);
                _context.LiquidManureSplits.Add(split);
            }

            record.VoucherOut = string.Join(", ", voucherList);

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
    }
}
