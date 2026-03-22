using Izabella.Services;
namespace Izabella.Controllers
{
    using Microsoft.AspNetCore.Mvc;

    public class CalcTestController : Controller
    {
        private readonly ManureCalculationService _calc;

        public CalcTestController(ManureCalculationService calc)
        {
            _calc = calc;
        }

        public IActionResult Index()
        {
            var liquid = _calc.CalculateLiquid(150);
            var solid = _calc.CalculateSolid(10000);

            return Json(new { liquid, solid });
        }
    }
}
