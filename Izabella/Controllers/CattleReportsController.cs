using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Izabella.Models;

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
    }
}