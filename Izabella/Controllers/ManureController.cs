using Izabella.Models;
using Izabella.Models.DTOs;
using Izabella.Services;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> SubmitSolidMulti([FromBody] List<SolidEntryDto> entries)
        {
            var result = await _solidService.ProcessDailyAsync(DateTime.Now, entries);
            return Json(result);
        }
        [HttpPost]
        public async Task<IActionResult> SubmitLiquid(double totalAmount)
        {
            var result = _calc.CalculateLiquid(totalAmount);

            // Bizonylat generálás
            var voucher = await _voucher.CreateVoucherAsync("+");
            var voucherCode = _voucher.FormatVoucher(voucher);

            // DB mentés (példa)
            var record = new LiquidManure
            {
                Date = System.DateTime.Now,
                TotalAmount = totalAmount,
                Cow = result.Cow,
                Young6_9 = result.Young6_9,
                Young9_12 = result.Young9_12,
                Young12Preg = result.Young12Preg,
                PregnantHeifer = result.PregnantHeifer,
                VoucherCode = voucherCode
            };
            _context.LiquidManures.Add(record);
            await _context.SaveChangesAsync();

            return Json(new { success = true, result, voucherCode });
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
