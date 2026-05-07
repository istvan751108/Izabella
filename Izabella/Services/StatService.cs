using Microsoft.EntityFrameworkCore;
using Izabella.Models;

namespace Izabella.Services
{
    public class StatService : IStatService
    {
        private readonly IzabellaDbContext _context; // Ellenőrizd a saját DbContext nevedet!

        public StatService(IzabellaDbContext context)
        {
            _context = context;
        }

        public async Task UpdateDailyStatAsync(DateTime startDate, int companyId, string ageGroup, int countChange, double weightChange)
        {
            // Egy évre előre frissítünk (vagy igény szerint módosítható)
            var endDate = DateTime.Now.Date.AddYears(1);

            // Itt a ToListAsync-nél jelezzük a típust, ha továbbra is hibát dobna
            var stats = await _context.DailyStats
                .Where(s => s.StatDate >= startDate.Date &&
                            s.StatDate <= endDate &&
                            s.CompanyId == companyId &&
                            s.AgeGroup == ageGroup)
                .ToListAsync<DailyStat>(); // <--- Explicit típusmegadás

            for (var date = startDate.Date; date <= endDate; date = date.AddDays(1))
            {
                var currentStat = stats.FirstOrDefault(s => s.StatDate == date);

                if (currentStat == null)
                {
                    // Keressük meg a legutolsó létező adatot a múltból
                    var prevStat = await _context.DailyStats
                        .Where(s => s.StatDate < date &&
                                    s.CompanyId == companyId &&
                                    s.AgeGroup == ageGroup)
                        .OrderByDescending(s => s.StatDate)
                        .FirstOrDefaultAsync<DailyStat>(); // <--- Explicit típusmegadás

                    var newStat = new DailyStat
                    {
                        StatDate = date,
                        CompanyId = companyId,
                        AgeGroup = ageGroup,
                        Count = (prevStat?.Count ?? 0) + countChange,
                        TotalWeight = (prevStat?.TotalWeight ?? 0) + weightChange
                    };

                    _context.DailyStats.Add(newStat);
                    // Hozzáadjuk a listához is, hogy a ciklus következő köre lássa
                    stats.Add(newStat);
                }
                else
                {
                    currentStat.Count += countChange;
                    currentStat.TotalWeight += weightChange;
                    _context.Update(currentStat);
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}