using Izabella.Services;
using Microsoft.AspNetCore.Mvc;
namespace Izabella.Controllers

{
    public class VoucherTestController : Controller
    {
        private readonly VoucherService _voucherService;

        public VoucherTestController(VoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        public async Task<IActionResult> Index()
        {
            var v1 = await _voucherService.CreateVoucherAsync("+");
            var v2 = await _voucherService.CreateVoucherAsync("-");

            var text = _voucherService.FormatVoucher(v1) + "<br>" +
                       _voucherService.FormatVoucher(v2);

            return Content(text, "text/html");
        }
    }
}