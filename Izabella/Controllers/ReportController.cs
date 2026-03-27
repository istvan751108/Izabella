using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Controllers
{
    public class ReportController : Controller
    {
        private readonly IzabellaDbContext _context;

        public ReportController(IzabellaDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? year, int? month)
        {
            var y = year ?? DateTime.Today.Year;
            var m = month ?? DateTime.Today.Month;

            var loads = await _context.SolidManureLoads
                .Where(x => x.Date.Year == y && x.Date.Month == m)
                .ToListAsync();

            var liquids = await _context.LiquidManures
                .Where(x => x.Date.Year == y && x.Date.Month == m)
                .ToListAsync();

            var solids = await _context.SolidManureDailies
                .Where(x => x.Date.Year == y && x.Date.Month == m)
                .ToListAsync();

            var yearlyLiquid = _context.LiquidManures
                .Where(x => x.Date.Year == y)
                .Sum(x => x.TotalAmount);

            var yearlySolid = _context.SolidManureDailies
                .Where(x => x.Date.Year == y)
                .Sum(x => x.TotalNet);

            var vm = new DailyReportVm
            {
                Year = y,
                Month = m,
                Liquids = liquids,
                Days = solids,
                Loads = loads, 
                LiquidTotal = liquids.Sum(x => x.TotalAmount),
                SolidTotal = solids.Sum(x => x.TotalNet),
                YearlyLiquid = yearlyLiquid,  
                YearlySolid = yearlySolid,
                Date = DateTime.Today
            };

            return View(vm);
        }
        public async Task<IActionResult> Monthly(int year, int month)
        {
            var data = await _context.LiquidManures
                .Where(x => x.Date.Year == year && x.Date.Month == month)
                .ToListAsync();

            return View(data);
        }
    }
}
