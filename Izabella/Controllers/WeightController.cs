using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Ez elengedhetetlen a ToListAsync-hez!

namespace Izabella.Controllers
{
    public class WeightController : Controller
    {
        private readonly IzabellaDbContext _context;
        public WeightController(IzabellaDbContext context) { _context = context; }

        [HttpGet]
        public IActionResult QuickWeightEntry() => View();

        [HttpPost]
        public async Task<IActionResult> SubmitWeight(string lastFour, decimal weight)
        {
            // Megkeressük az állatot az utolsó 4 számjegy alapján
            // (A betűket és az elejét levágjuk a kereséshez)
            var animal = await _context.Cattles
                .Where(c => c.IsActive)
                .ToListAsync(); // Beolvassuk, hogy C#-ban szűrhessünk a végére

            var target = animal.FirstOrDefault(c => c.EarTag.EndsWith(lastFour));

            if (target == null) return Json(new { success = false, message = "Nincs ilyen fülszám!" });

            var buffer = new WeightBuffer
            {
                EarTag = target.EarTag,
                Weight = weight,
                MeasuredDate = DateTime.Now
            };

            _context.WeightBuffers.Add(buffer);
            await _context.SaveChangesAsync();

            return Json(new { success = true, earTag = target.EarTag });
        }
    }
}
