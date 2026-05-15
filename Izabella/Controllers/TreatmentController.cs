using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Controllers
{
    public class TreatmentController : Controller
    {
        private readonly IzabellaDbContext _context;

        // Ez a rész hiányzott: a Dependency Injection ezen keresztül adja át az adatbázis elérést
        public TreatmentController(IzabellaDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> DryOffCandidates()
        {
            var today = DateTime.Today;

            var pregnantCattle = await _context.Cattles
                .Where(c => c.IsActive && c.PregnancyStatus == PregnancyStatus.Vemhes && c.LastInseminationDate != null)
                .ToListAsync();

            // 1. Összeállítjuk a listát
            var candidateList = pregnantCattle.Select(c => new DryOffCandidateViewModel
            {
                Cattle = c,
                ExpectedCalving = c.LastInseminationDate.Value.AddDays(276),
                DaysUntilCalving = (c.LastInseminationDate.Value.AddDays(276) - today).Days
            })
            .Where(x => x.DaysUntilCalving >= 55 && x.DaysUntilCalving <= 85)
            .OrderBy(x => x.DaysUntilCalving)
            .ToList();

            // 2. Létrehozzuk a View-nak szükséges ActionViewModel-t
            var vm = new DryOffActionViewModel
            {
                Candidates = candidateList,
                DryOffDate = DateTime.Today // Alapértelmezett dátum
            };

            ViewBag.Medications = await _context.Medications
                .Where(m => m.Category == TreatmentCategory.Apasztás && m.Quantity > 0)
                .ToListAsync();

            // 3. A VM-et adjuk át a View-nak, nem a sima listát!
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmDryOff(int cattleId, int medicationId, double dose, DateTime dryOffDate)
        {
            var medication = await _context.Medications.FindAsync(medicationId);
            var cattle = await _context.Cattles.FindAsync(cattleId);

            if (medication == null || medication.Quantity < dose)
            {
                ModelState.AddModelError("", "Nincs elég gyógyszer készleten!");
                return RedirectToAction("DryOffCandidates");
            }

            // 1. Apasztási esemény rögzítése
            var dryOffEvent = new DryOffEvent
            {
                CattleId = cattleId,
                MedicationId = medicationId,
                DryOffDate = dryOffDate,
                IsSeparated = false, // Kezdetben még nincs elkülönítve
                Note = "Keddi rutinszerű apasztás"
            };

            // 2. Készlet levonása
            medication.Quantity -= dose;
            _context.Entry(medication).State = EntityState.Modified;
            _context.DryOffEvents.Add(dryOffEvent);
            await _context.SaveChangesAsync();

            return RedirectToAction("DryOffCandidates");
        }

        [HttpPost]
        public async Task<IActionResult> ProcessDryOff(List<int> SelectedCattleIds, List<int> SelectedMedicationIds, DateTime DryOffDate)
        {
            if (SelectedCattleIds == null || !SelectedCattleIds.Any())
            {
                TempData["Error"] = "Nincs kijelölve tehén!";
                return RedirectToAction("DryOffCandidates");
            }

            foreach (var cattleId in SelectedCattleIds)
            {
                var cattle = await _context.Cattles.FindAsync(cattleId);
                if (cattle == null) continue;

                // Minden kijelölt gyógyszerre rögzítünk egy eseményt és levonjuk a készletet
                foreach (var medId in SelectedMedicationIds)
                {
                    var medication = await _context.Medications.FindAsync(medId);
                    if (medication != null && medication.Quantity >= medication.DefaultDose)
                    {
                        var dryEvent = new DryOffEvent
                        {
                            CattleId = cattleId,
                            MedicationId = medId,
                            DryOffDate = DryOffDate,
                            IsSeparated = false,
                            Note = "Tömeges apasztás"
                        };

                        medication.Quantity -= medication.DefaultDose;
                        _context.DryOffEvents.Add(dryEvent);
                    }
                }

                // Állat státuszának frissítése (pl. IsActive marad, de jelezhetjük, hogy már nem fejős)
                // Itt beállíthatunk egy flaget, vagy a SeparationDate-et használhatjuk később
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{SelectedCattleIds.Count} állat apasztása sikeresen rögzítve.";

            return RedirectToAction("DryOffCandidates");
        }
    }
}