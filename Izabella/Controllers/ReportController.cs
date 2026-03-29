using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Controllers
{
    [Authorize]
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
                .Include(l => l.Splits)
                .Where(l => l.Date.Year == y && l.Date.Month == m)
                .ToListAsync();

            var solids = await _context.SolidManureDailies
                .Where(x => x.Date.Year == y && x.Date.Month == m)
                .ToListAsync();

            // Csak az alap összesítések kellenek a táblázat aljára
            var vm = new DailyReportVm
            {
                Year = y,
                Month = m,
                Liquids = liquids,
                Days = solids,
                Loads = loads,
                LiquidTotal = liquids.Sum(x => x.TotalAmount),
                SolidTotal = solids.Sum(x => x.TotalNet),
                YearlyLiquid = _context.LiquidManures.Where(x => x.Date.Year == y).Sum(x => x.TotalAmount),
                YearlySolid = _context.SolidManureDailies.Where(x => x.Date.Year == y).Sum(x => x.TotalNet),
                Date = DateTime.Today
            };
            return View(vm);
        }
        public async Task<IActionResult> Charts(int? year, int? month)
        {
            var y = year ?? DateTime.Today.Year;
            var m = month ?? DateTime.Today.Month;

            // 1. Napi adatok az adott hónaphoz (A régi Index logikája alapján)
            var daysInMonth = DateTime.DaysInMonth(y, m);
            var dailyLabels = Enumerable.Range(1, daysInMonth).Select(day => day.ToString()).ToList();
            var liquidDailyData = new double[daysInMonth];
            var solidDailyData = new double[daysInMonth];

            var monthlyLiquids = await _context.LiquidManures.Where(l => l.Date.Year == y && l.Date.Month == m).ToListAsync();
            var monthlySolids = await _context.SolidManureDailies.Where(x => x.Date.Year == y && x.Date.Month == m).ToListAsync();

            foreach (var item in monthlyLiquids) liquidDailyData[item.Date.Day - 1] += item.TotalAmount;
            foreach (var item in monthlySolids) solidDailyData[item.Date.Day - 1] += item.TotalNet;

            // 2. Éves havi bontás (1-12 hónap)
            var yearlyLiquidData = new double[12];
            var yearlySolidData = new double[12];
            var allYearLiquids = await _context.LiquidManures.Where(x => x.Date.Year == y).ToListAsync();
            var allYearSolids = await _context.SolidManureDailies.Where(x => x.Date.Year == y).ToListAsync();

            foreach (var item in allYearLiquids) yearlyLiquidData[item.Date.Month - 1] += item.TotalAmount;
            foreach (var item in allYearSolids) yearlySolidData[item.Date.Month - 1] += item.TotalNet;

            // 3. Összehasonlítás
            var compLabels = new List<string> { (y - 2).ToString(), (y - 1).ToString(), y.ToString() };
            var compSolid = new List<double>();
            var compLiquid = new List<double>();
            for (int i = 2; i >= 0; i--)
            {
                compSolid.Add(await _context.SolidManureDailies.Where(x => x.Date.Year == (y - i)).SumAsync(x => x.TotalNet));
                compLiquid.Add(await _context.LiquidManures.Where(x => x.Date.Year == (y - i)).SumAsync(x => x.TotalAmount));
            }

            ViewBag.Year = y;
            ViewBag.Month = m;
            ViewBag.DailyLabels = dailyLabels;
            ViewBag.LiquidDaily = liquidDailyData;
            ViewBag.SolidDaily = solidDailyData;
            ViewBag.YearlyLiquid = yearlyLiquidData;
            ViewBag.YearlySolid = yearlySolidData;
            ViewBag.CompLabels = compLabels;
            ViewBag.CompSolid = compSolid;
            ViewBag.CompLiquid = compLiquid;

            return View();
        }
    }
}
