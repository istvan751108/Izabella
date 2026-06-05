using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Izabella.Controllers
{
    public class TreatmentController : Controller
    {
        private readonly IzabellaDbContext _context;
        private readonly IConfiguration _configuration;

        // Ez a rész hiányzott: a Dependency Injection ezen keresztül adja át az adatbázis elérést
        public TreatmentController(IzabellaDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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

            if (cattle == null) return NotFound();
            if (medication == null || medication.Quantity < dose)
            {
                TempData["Error"] = "Nincs elég gyógyszer készleten!";
                return RedirectToAction("DryOffCandidates");
            }

            // 1. Apasztási esemény rögzítése
            var dryOffEvent = new DryOffEvent
            {
                CattleId = cattleId,
                MedicationId = medicationId,
                DryOffDate = dryOffDate,
                IsSeparated = false,
                Note = "Keddi rutinszerű apasztás"
            };
            _context.DryOffEvents.Add(dryOffEvent);

            // 2. Készlet levonása
            medication.Quantity -= dose;
            _context.Entry(medication).State = EntityState.Modified;

            // 3. JAVÍTÁS: ANIMAL HISTORY BEJEGYZÉS (Gyógykezelés naplózása az ÉVI adatokkal)
            DateTime? milkExpiry = medication.WithdrawalPeriodMilk > 0 ? dryOffDate.AddDays(medication.WithdrawalPeriodMilk) : null;
            DateTime? meatExpiry = medication.WithdrawalPeriodMeat > 0 ? dryOffDate.AddDays(medication.WithdrawalPeriodMeat) : null;

            var history = new AnimalHistory
            {
                CattleId = cattleId,
                EventDate = dryOffDate,
                Type = "Gyógykezelés", // Szigorúan az általad megadott struktúra szerint
                Comment = $"[Apasztás] Lekezelve: {medication.Name} ({dose} {medication.Unit}). " +
                          $"Tej lejár: {milkExpiry?.ToShortDateString() ?? "Ellés utáni fejések"}, " +
                          $"Hús lejár: {meatExpiry?.ToShortDateString() ?? "Nincs"}. Megj: Keddi rutinszerű apasztás",
                IsEnarReported = false
            };
            _context.Add(history);

            await _context.SaveChangesAsync();
            TempData["Success"] = $" {cattle.EarTag} apasztása és gyógyszerelése rögzítve.";
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
            if (SelectedMedicationIds == null || !SelectedMedicationIds.Any())
            {
                TempData["Error"] = "Nincs kijelölve apasztó készítmény!";
                return RedirectToAction("DryOffCandidates");
            }

            // JAVÍTÁS: Előzetes készletellenőrzés a tömeges mentésnél a negatív raktár elkerülésére
            double totalRequiredDose = 0;
            foreach (var medId in SelectedMedicationIds)
            {
                var medication = await _context.Medications.FindAsync(medId);
                if (medication != null)
                {
                    totalRequiredDose = medication.DefaultDose * SelectedCattleIds.Count;
                    if (medication.Quantity < totalRequiredDose)
                    {
                        TempData["Error"] = $"Nincs elég készlet a(z) {medication.Name} termékből! Szükséges: {totalRequiredDose} {medication.Unit}, Elérhető: {medication.Quantity}";
                        return RedirectToAction("DryOffCandidates");
                    }
                }
            }

            // Ha mindenből van elég, jöhet a mentés
            foreach (var cattleId in SelectedCattleIds)
            {
                var cattle = await _context.Cattles.FindAsync(cattleId);
                if (cattle == null) continue;

                foreach (var medId in SelectedMedicationIds)
                {
                    var medication = await _context.Medications.FindAsync(medId);
                    if (medication != null)
                    {
                        var dryEvent = new DryOffEvent
                        {
                            CattleId = cattleId,
                            MedicationId = medId,
                            DryOffDate = DryOffDate,
                            IsSeparated = false,
                            Note = "Tömeges apasztás"
                        };
                        _context.DryOffEvents.Add(dryEvent);

                        // Levonás
                        medication.Quantity -= medication.DefaultDose;

                        // JAVÍTÁS: History bejegyzés generálása minden állatnak a gyógyszerről
                        DateTime? milkExpiry = medication.WithdrawalPeriodMilk > 0 ? DryOffDate.AddDays(medication.WithdrawalPeriodMilk) : null;
                        DateTime? meatExpiry = medication.WithdrawalPeriodMeat > 0 ? DryOffDate.AddDays(medication.WithdrawalPeriodMeat) : null;

                        var history = new AnimalHistory
                        {
                            CattleId = cattleId,
                            EventDate = DryOffDate,
                            Type = "Gyógykezelés",
                            Comment = $"[Tömeges Apasztás] Készítmény: {medication.Name}. " +
                                      $"Tej ÉVI: {milkExpiry?.ToShortDateString() ?? "Ellés után"}, " +
                                      $"Hús ÉVI: {meatExpiry?.ToShortDateString() ?? "Nincs"}.",
                            IsEnarReported = false
                        };
                        _context.Add(history);
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{SelectedCattleIds.Count} állat apasztása sikeresen rögzítve, kórtörténet frissítve.";
            return RedirectToAction("DryOffCandidates");
        }

        public async Task<IActionResult> SeparationList()
        {
            // Azokat keressük, akiknél rögzítve van apasztási esemény, de még nincsenek elválasztva
            var toSeparate = await _context.DryOffEvents
                .Include(d => d.Cattle)
                .Where(d => !d.IsSeparated)
                .OrderBy(d => d.DryOffDate)
                .ToListAsync();

            return View(toSeparate);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmSeparation(List<int> eventIds)
        {
            if (eventIds == null || !eventIds.Any())
            {
                TempData["Error"] = "Nincs kijelölve állat az elkülönítésre!";
                return RedirectToAction("SeparationList");
            }

            string dryOffStall = _configuration["FarmSettings:DefaultDryOffStallCode"] ?? "5";

            var events = await _context.DryOffEvents
                .Include(e => e.Cattle)
                .Where(e => eventIds.Contains(e.Id))
                .ToListAsync();

            foreach (var ev in events)
            {
                if (ev.Cattle == null) continue;

                ev.IsSeparated = true;
                ev.SeparationDate = DateTime.Now;

                string customNote = Request.Form[$"notes_{ev.Id}"];
                if (!string.IsNullOrEmpty(customNote))
                {
                    ev.Note = customNote;
                }

                // Mentjük a régi adatokat a történethez a változás előtt
                string? oldStall = ev.Cattle.Stall;
                string? oldAgeGroup = ev.Cattle.AgeGroup;

                // Az állat aktuális státuszának átírása
                ev.Cattle.Stall = dryOffStall;
                ev.Cattle.AgeGroup = "Szárazonálló tehén";

                // JAVÍTÁS: HerdId kicserélve CurrentHerdId-ra, hogy egyezzen a Cattle modellel
                var history = new AnimalHistory
                {
                    CattleId = ev.CattleId,
                    EventDate = DateTime.Today,
                    Type = "Áthelyezés",
                    OldAgeGroup = oldAgeGroup,
                    NewAgeGroup = "Szárazonálló tehén",
                    OldHerdId = ev.Cattle.CurrentHerdId, // ITT JAVÍTVA
                    NewHerdId = ev.Cattle.CurrentHerdId, // ITT JAVÍTVA
                    StallName = dryOffStall,
                    Comment = $"Áthelyezve a fejősök közül a szárazonállókhoz. " + (!string.IsNullOrEmpty(ev.Note) ? $" Megj: {ev.Note}" : ""),
                    IsEnarReported = false
                };
                _context.Add(history);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{events.Count} állat fizikai áthelyezése megtörtént, a történeti napló frissítve.";

            return RedirectToAction("SeparationList");
        }

        [HttpPost]
        public async Task<IActionResult> ReconcileDryOff(IFormFile difFile)
        {
            if (difFile == null || difFile.Length == 0)
            {
                TempData["Error"] = "Kérlek, válassz ki egy érvényes Afimilk .dif fájlt!";
                return RedirectToAction("ReconciliationDashboard");
            }

            // 1. Gyűjtsük ki az Afimilk fájlból a szárazonálló fülszámokat (betűvel együtt, pl. "T3253")
            var afiDryEarTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var reader = new StreamReader(difFile.OpenReadStream()))
            {
                string line;
                bool dataSectionStarted = false;
                int valueCounter = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (line == "DATA")
                    {
                        dataSectionStarted = true;
                        continue;
                    }

                    if (!dataSectionStarted) continue;

                    if (line == "V")
                    {
                        valueCounter = 0;
                        continue;
                    }

                    if (line.StartsWith("1,0") || line.StartsWith("0 REG") || line.StartsWith("0,"))
                    {
                        string dataLine = reader.ReadLine()?.Trim()?.Replace("\"", "");
                        valueCounter++;

                        // A 2. adatpozíció a Tehén fülazonosítója betűvel együtt (pl. "T3253")
                        if (valueCounter == 2 && !string.IsNullOrEmpty(dataLine))
                        {
                            afiDryEarTags.Add(dataLine); // Tisztítás nélkül, egy az egyben eltároljuk
                        }
                    }
                }
            }

            // 2. Kérjük le az Izabella adatbázisból az AKTULÁISAN szárazonállóként nyilvántartott állatokat
            string dryOffStall = _configuration["FarmSettings:DefaultDryOffStallCode"] ?? "5";

            var izabellaDryCattle = await _context.Cattles
                .Where(c => c.Stall == dryOffStall)
                .Select(c => c.EarTag)
                .ToListAsync();

            var discrepancies = new List<DiscrepancyItem>();

            // 3. ÖSSZEVETÉS LOGIKÁJA (Közvetlen egyezőség vizsgálata a betű+4szám formátum alapján)

            // A) Az Izabellában szárazonálló (5-ös istálló), de az Afimilk fájlból HIÁNYZIK
            foreach (var earTag in izabellaDryCattle)
            {
                if (!afiDryEarTags.Contains(earTag))
                {
                    discrepancies.Add(new DiscrepancyItem
                    {
                        EarTag = earTag,
                        IzabellaStatus = $"Szárazonálló ({dryOffStall}-ös istálló)",
                        AfimilkStatus = "Fejős / Egyéb csoport",
                        DiscrepancyType = "MissingFromAfi",
                        Message = "Az Izabellában már elapasztottuk, de az állatorvos még nem rögzítette az Afimilkben!"
                    });
                }
            }

            // B) Az Afimilkben szárazonálló (5-ös csoport), de az Izabella szerint még FEJŐS (nincs az 5-ös istállóban)
            foreach (var earTag in afiDryEarTags)
            {
                var cattleInIzabella = await _context.Cattles
                    .FirstOrDefaultAsync(c => c.EarTag == earTag);

                if (cattleInIzabella != null && cattleInIzabella.Stall != dryOffStall)
                {
                    discrepancies.Add(new DiscrepancyItem
                    {
                        EarTag = earTag,
                        IzabellaStatus = $"Fejős ({cattleInIzabella.Stall} istálló)",
                        AfimilkStatus = "Szárazonálló (5-ös csoport)",
                        DiscrepancyType = "UnexpectedInAfi",
                        Message = "Az Afimilkben már szárazonálló, de az Izabellában elmaradt a kezelés vagy az elkülönítés rögzítése!"
                    });
                }
            }

            return View("ReconciliationResult", discrepancies);
        }

        [HttpGet]
        public IActionResult Reconciliation()
        {
            return View();
        }
    }
}