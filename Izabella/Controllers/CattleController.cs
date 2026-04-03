using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Izabella.Models;

namespace Izabella.Controllers
{
    public class CattleController : Controller
    {
        private readonly IzabellaDbContext _context;

        public CattleController(IzabellaDbContext context)
        {
            _context = context;
        }

        // --- 1. AZ ELLÉS ŰRLAP MEGJELENÍTÉSE ---
        [HttpGet]
        public IActionResult Calving()
        {
            // Csak azokat a tenyészeteket adjuk át, ahol van beállítva Prefix (ahol lehet ellés)
            var birthHerds = _context.Herds
                .Where(h => !string.IsNullOrEmpty(h.DefaultPrefix))
                .Select(h => new { h.Id, Name = h.Name + " (" + h.HerdCode + ")" })
                .ToList();

            ViewBag.Herds = new SelectList(birthHerds, "Id", "Name");
            return View();
        }

        // --- 2. AZ ANYA ELLENŐRZÉSE (AJAX híváshoz) ---
        [HttpGet]
        public async Task<IActionResult> GetDamInfo(string earTag)
        {
            var dam = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .FirstOrDefaultAsync(c => c.EarTag == earTag);

            if (dam == null) return NotFound();

            return Json(new
            {
                earTag = dam.EarTag,
                enar = dam.EnarNumber,
                ageGroup = dam.DamAgeAtCalving ?? (dam.Gender == Gender.Üsző ? "Üsző" : "Tehén"),
                herd = dam.CurrentHerd?.Name
            });
        }

        // --- 3. KÖVETKEZŐ FÜLSZÁM ÉS ENAR GENERÁLÁSA (AJAX) ---
        [HttpGet]
        public async Task<IActionResult> GetNextIdentifiers(int herdId)
        {
            var herd = await _context.Herds.FindAsync(herdId);
            if (herd == null || string.IsNullOrEmpty(herd.DefaultPrefix)) return BadRequest();

            // Megkeressük az adott prefixszel rendelkező legutolsó állatot
            var lastCattle = await _context.Cattles
                .Where(c => c.EarTag.StartsWith(herd.DefaultPrefix))
                .OrderByDescending(c => c.EarTag)
                .FirstOrDefaultAsync();

            int nextNum = 1;
            if (lastCattle != null && lastCattle.EarTag.Length > 1)
            {
                if (int.TryParse(lastCattle.EarTag.Substring(1), out int lastNum))
                {
                    nextNum = lastNum + 1;
                }
            }

            string newEarTag = herd.DefaultPrefix + nextNum.ToString("D4");
            string newEnar = GenerateEnar(newEarTag, herd.EnarPrefix ?? "35984");

            return Json(new { earTag = newEarTag, enar = newEnar });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCalf(Cattle calf, string EarTag2, string EnarNumber2, string Gender2, double? BirthWeight2, string IsTwin)
        {
            // Kézzel beállítjuk a bool értéket, mert a böngésző "on"-t küldhet
            calf.IsTwin = (IsTwin == "on" || IsTwin == "true");
            // Megkeressük az anyát a cégadatokhoz
            var dam = await _context.Cattles.FirstOrDefaultAsync(c => c.EnarNumber == calf.MotherEnar);
            if (dam != null)
            {
                calf.CompanyId = dam.CompanyId;
            }

            // ELTÁVOLÍTJUK A KRITIKUS MEZŐKET A VALIDÁCIÓBÓL
            ModelState.Remove("CurrentHerd");
            ModelState.Remove("Company");
            ModelState.Remove("PassportNumber"); // Ez okozta a hibát
            ModelState.Remove("AgeGroup");       // Ez is okozta a hibát
            ModelState.Remove("EarTag2");
            ModelState.Remove("EnarNumber2");
            ModelState.Remove("IsTwin");
            // Ha van más "Field is required" hiba, azt is add hozzá ide ModelState.Remove("MezőNeve") formában!

            if (ModelState.IsValid)
            {
                try
                {
                    // Alapadatok beállítása mentés előtt
                    calf.IsActive = true;
                    calf.IsAlive = true;
                    calf.PassportNumber = "KÉRVE";
                    calf.AgeGroup = (calf.Gender == Gender.Bika ? "Bika borjú" : "Üsző borjú");

                    _context.Cattles.Add(calf);

                    if (calf.IsTwin && !string.IsNullOrEmpty(EarTag2))
                    {
                        var secondCalf = new Cattle
                        {
                            EarTag = EarTag2,
                            EnarNumber = EnarNumber2,
                            Gender = (Gender2 == "Bika" ? Gender.Bika : Gender.Üsző),
                            BirthWeight = BirthWeight2 ?? 35,
                            BirthDate = calf.BirthDate,
                            MotherEnar = calf.MotherEnar,
                            CurrentHerdId = calf.CurrentHerdId,
                            CompanyId = calf.CompanyId,
                            IsActive = true,
                            IsAlive = true,
                            IsTwin = true,
                            PassportNumber = "KÉRVE",
                            AgeGroup = (Gender2 == "Bika" ? "Bika borjú" : "Üsző borjú")
                        };
                        _context.Cattles.Add(secondCalf);
                    }

                    // 3. Anya frissítése és Vemhesség lezárása
                    //var dam = await _context.Cattles.FirstOrDefaultAsync(c => c.EnarNumber == calf.MotherEnar);
                    if (dam != null)
                    {
                        dam.DamAgeAtCalving = "Tehén";
                        _context.Update(dam);

                        var activeBreeding = await _context.BreedingDatas
                            .Where(b => b.CattleId == dam.Id && b.IsPregnant == true)
                            .OrderByDescending(b => b.PregnancyTestDate)
                            .FirstOrDefaultAsync();

                        if (activeBreeding != null)
                        {
                            activeBreeding.IsPregnant = false;
                            _context.Update(activeBreeding);
                        }
                    }

                    await _context.SaveChangesAsync();
                    return RedirectToAction("Index", "Cattles"); // Átvisz az állatlistához
                }
                catch (Exception ex)
{
    // Ez kiírja a belső hibaüzenetet is, amiből látni fogjuk a konkrét SQL hibát
    var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
    ModelState.AddModelError("", "Adatbázis hiba: " + innerMessage);
}
            }

            // Ha idáig eljutunk, hiba volt, újra kell tölteni a listákat
            var birthHerds = _context.Herds.Where(h => !string.IsNullOrEmpty(h.DefaultPrefix)).ToList();
            ViewBag.Herds = new SelectList(birthHerds, "Id", "Name");
            return View("Calving", calf);
        }

        // --- ENAR GENERÁLÓ LOGIKA (A VBA kódod alapján) ---
        private string GenerateEnar(string earTag, string enarPrefix)
        {
            string numericPart = new string(earTag.Where(char.IsDigit).ToArray());
            string baseNumber = enarPrefix + numericPart.PadLeft(4, '0');

            int[] weights = { 3, 1, 7, 9, 3, 1, 7, 9, 3 };
            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                sum += (baseNumber[i] - '0') * weights[i];
            }
            int checksum = (10 - (sum % 10)) % 10;
            return "HU" + baseNumber + checksum;
        }
        [HttpGet]
        public IActionResult CalculateEnar(string earTag, int herdId)
        {
            var herd = _context.Herds.Find(herdId);
            if (herd == null) return BadRequest();

            string enar = GenerateEnar(earTag, herd.EnarPrefix ?? "35984");
            return Json(new { enar = enar });
        }
    }
}
