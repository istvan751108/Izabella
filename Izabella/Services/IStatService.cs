namespace Izabella.Services
{
    public interface IStatService
    {
        // Ezt hívod meg minden eseménynél
        Task UpdateDailyStatAsync(DateTime startDate, int companyId, string ageGroup, int countChange, double weightChange);
    }
}
