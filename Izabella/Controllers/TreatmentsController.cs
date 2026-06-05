using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

public class TreatmentsController : Controller
{
    private readonly IzabellaDbContext _context;

    public TreatmentsController(IzabellaDbContext context)
    {
        _context = context;
    }

    // A FŐOLDAL: Itt jelenik meg a 4 nagy gomb/kapcsoló (Tőgy, Láb, Szaporodás, Egyéb)
    public IActionResult Index()
    {
        return View();
    }

    // GET: Új kezelés rögzítése egy konkrét kategóriához
    public async Task<IActionResult> Create(TreatmentCategory category)
    {
        // Aktív tehenek / üszők listája a kiválasztáshoz (minden kategóriának kell)
        var activeCattle = await _context.Cattles
            .Where(c => c.IsActive)
            .OrderBy(c => c.EarTag)
            .Select(c => new { c.Id, DisplayName = c.EarTag + " - " + c.EnarNumber })
            .ToListAsync();

        ViewBag.CattleId = new SelectList(activeCattle, "Id", "DisplayName");

        // HA TŐGYKEZELÉSRŐL VAN SZÓ:
        if (category == TreatmentCategory.Tőgy)
        {
            // Feltöltjük a speciális tőgykezelési legördülő listákat (Medications, SecondaryMedications, stb.)
            PopulateDropLists(TreatmentCategory.Tőgy);

            // Létrehozzuk a Mastitis-specifikus ViewModelt az inicializált sorokkal
            var mastitisModel = new MastitisTreatmentViewModel
            {
                Rows = new List<MastitisTreatmentRowViewModel>
            {
                new MastitisTreatmentRowViewModel { Date = DateTime.Today } // Egy üres kezdősor
            }
            };

            return View("CreateTőgy", mastitisModel);
        }

        // MINDEN MÁS KATEGÓRIA ESETÉN (Láb, Szaporodás, Egyéb):
        var availableMedications = await _context.Medications
            .Where(m => m.Category == category && m.Quantity > 0)
            .ToListAsync();

        ViewBag.MedicationId = new SelectList(availableMedications, "Id", "Name");

        var model = new AnimalTreatment
        {
            Category = category,
            TreatmentDate = DateTime.Today
        };

        return View($"Create{category}", model);
    }

    // POST: Kezelés mentése, ÉVI számítás, raktár levonás, History mentés
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnimalTreatment treatment)
    {
        if (ModelState.IsValid)
        {
            var medication = await _context.Medications.FindAsync(treatment.MedicationId);
            if (medication == null) return NotFound();

            // 1. Raktárkészlet ellenőrzése és levonása
            if (medication.Quantity < treatment.AdministeredDose)
            {
                ModelState.AddModelError("AdministeredDose", $"Nincs elég készlet! Elérhető: {medication.Quantity} {medication.Unit}");
                PopulateDropLists(treatment.Category);
                return View($"Create{treatment.Category}", treatment);
            }

            medication.Quantity -= treatment.AdministeredDose;

            // 2. Várakozási idők kiszámítása (Kezelés napja + ÉVI napok száma)
            if (medication.WithdrawalPeriodMilk > 0)
            {
                treatment.MilkWithdrawalExpiry = treatment.TreatmentDate.AddDays(medication.WithdrawalPeriodMilk);
            }
            if (medication.WithdrawalPeriodMeat > 0)
            {
                treatment.MeatWithdrawalExpiry = treatment.TreatmentDate.AddDays(medication.WithdrawalPeriodMeat);
            }

            // 3. Kezelés mentése az új központi kezelési táblába
            _context.Add(treatment);

            // 4. ANIMAL HISTORY MENTÉSE - AZ UTOLSÓ KORREKCIÓD ALAPJÁN PONTOSÍTVA:
            var history = new AnimalHistory
            {
                CattleId = treatment.CattleId,
                EventDate = treatment.TreatmentDate,
                Type = "Gyógykezelés", // Fixen beállítva a típus
                Comment = $"[{treatment.Category}] Kezelve: {medication.Name} ({treatment.AdministeredDose} {medication.Unit}). " +
                          $"Tej ÉVI lejár: {treatment.MilkWithdrawalExpiry?.ToShortDateString() ?? "Nincs"}, " +
                          $"Hús ÉVI lejár: {treatment.MeatWithdrawalExpiry?.ToShortDateString() ?? "Nincs"}." +
                          (!string.IsNullOrEmpty(treatment.Note) ? $" Megj: {treatment.Note}" : ""),
                IsEnarReported = false // A gyógykezelést nem kell az ENAR felé jelenteni, mint egy eladást vagy áthelyezést
            };
            _context.Add(history);

            await _context.SaveChangesAsync();
            TempData["Success"] = "A gyógykezelés sikeresen rögzítve, a raktárkészlet frissítve!";
            return RedirectToAction(nameof(Index));
        }

        PopulateDropLists(treatment.Category);
        return View($"Create{treatment.Category}", treatment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMastitis(MastitisTreatmentViewModel vm)
    {
        if (!ModelState.IsValid || vm.Rows == null || !vm.Rows.Any())
        {
            TempData["Error"] = "A kezelési adatok hibásak vagy üresek!";
            // JAVÍTÁS: string helyett az Enum típus átadása a segédmetódusnak
            return RedirectToAction(nameof(Create), new { category = TreatmentCategory.Tőgy });
        }

        var cattle = await _context.Cattles.FindAsync(vm.CattleId);
        if (cattle == null) return NotFound();

        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                foreach (var row in vm.Rows)
                {
                    var med1 = await _context.Medications.FindAsync(row.MedicationId);
                    if (med1 == null) continue;

                    // 1. Elsődleges gyógyszer készletellenőrzés és levonás
                    double actualDose1 = med1.DefaultDose > 0 ? med1.DefaultDose : 1;
                    if (med1.Quantity < actualDose1)
                    {
                        throw new Exception($"Nincs elég készlet a(z) {med1.Name} gyógyszerből! Szükséges: {actualDose1}, Elérhető: {med1.Quantity}");
                    }
                    med1.Quantity -= actualDose1;

                    // Várakozási idők kalkulációja az 1. szerre
                    DateTime? milkExpiry = med1.WithdrawalPeriodMilk > 0 ? row.Date.AddDays(med1.WithdrawalPeriodMilk) : null;
                    DateTime? meatExpiry = med1.WithdrawalPeriodMeat > 0 ? row.Date.AddDays(med1.WithdrawalPeriodMeat) : null;

                    // 2. Opcionális másodlagos gyógyszer (Kombináció, pl. Eurofit) kezelése
                    var med2 = row.SecondaryMedicationId.HasValue ? await _context.Medications.FindAsync(row.SecondaryMedicationId.Value) : null;
                    string comboText = "";

                    if (med2 != null)
                    {
                        double actualDose2 = med2.DefaultDose > 0 ? med2.DefaultDose : 10;
                        if (med2.Quantity < actualDose2)
                        {
                            throw new Exception($"Nincs elég készlet a kombinált {med2.Name} gyógyszerből!");
                        }
                        med2.Quantity -= actualDose2;
                        comboText = $" + {med2.Name} ({actualDose2} {med2.Unit})";

                        if (med2.WithdrawalPeriodMilk > 0)
                        {
                            DateTime med2MilkExpiry = row.Date.AddDays(med2.WithdrawalPeriodMilk);
                            if (milkExpiry == null || med2MilkExpiry > milkExpiry) milkExpiry = med2MilkExpiry;
                        }
                        if (med2.WithdrawalPeriodMeat > 0)
                        {
                            DateTime med2MeatExpiry = row.Date.AddDays(med2.WithdrawalPeriodMeat);
                            if (meatExpiry == null || med2MeatExpiry > meatExpiry) meatExpiry = med2MeatExpiry;
                        }
                    }

                    // 3. JAVÍTÁS: Modellhez igazítás (Category Enum lett, Diagnosis beolvasztva a Note-ba)
                    var treatment = new AnimalTreatment
                    {
                        CattleId = vm.CattleId,
                        TreatmentDate = row.Date,
                        Category = TreatmentCategory.Tőgy, // ITT JAVÍTVA (Enum érték)
                        MedicationId = row.MedicationId,
                        AdministeredDose = actualDose1,
                        MilkWithdrawalExpiry = milkExpiry,
                        MeatWithdrawalExpiry = meatExpiry,
                        // ITT JAVÍTVA: A diagnózist és a negyedet összefűzzük a Note mezőbe
                        Note = $"[{vm.Diagnosis}] Érintett negyed: {row.Quarter}.{comboText} {row.Note}"
                    };
                    _context.AnimalTreatments.Add(treatment);

                    // 4. AnimalHistory bejegyzés generálása (Típus: "Gyógykezelés")
                    var history = new AnimalHistory
                    {
                        CattleId = vm.CattleId,
                        EventDate = row.Date,
                        Type = "Gyógykezelés",
                        Comment = $"[Tőgykezelés - {row.Quarter}] {med1.Name} ({actualDose1} {med1.Unit}){comboText}. " +
                                  $"Tej zárlat: {milkExpiry?.ToShortDateString() ?? "Nincs / Ellés után"}, " +
                                  $"Hús zárlat: {meatExpiry?.ToShortDateString() ?? "Nincs"}.",
                        IsEnarReported = false
                    };
                    _context.AnimalHistories.Add(history);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "A teljes tőgykezelési sorozat rögzítése és a raktárkészlet frissítése sikeresen megtörtént!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = $"Hiba történt a mentés során: {ex.Message}";
            }
        }

        // JAVÍTÁS: string helyett Enum-ot adunk át az újratöltéskor is
        PopulateDropLists(TreatmentCategory.Tőgy);
        return View("CreateTőgy", vm);
    }

    // GET: Treatments/CreateLáb
    public IActionResult CreateLáb()
    {
        var model = new BulkFootTreatmentViewModel
        {
            TreatmentDate = DateTime.Today
        };
        return View(model);
    }

    // POST: Treatments/CreateLáb
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLáb(BulkFootTreatmentViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        // 1. A beolvasott szöveg feldolgozása (szétvágás új sor, vessző vagy szóköz mentén)
        var inputTags = vm.RawEarTags
            .Split(new[] { '\r', '\n', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Distinct()
            .ToList();

        if (!inputTags.Any())
        {
            ModelState.AddModelError("RawEarTags", "Nem sikerült értékelhető fül- vagy ENAR számot kivonni a szövegből.");
            return View(vm);
        }

        // 2. Megkeressük az adatbázisban azokat az állatokat, amelyek egyeznek akár a Rövid Fülszámmal, akár a Hosszú ENAR számmal
        var matchedCattle = await _context.Cattles
            .Where(c => c.IsActive && (inputTags.Contains(c.EarTag) || inputTags.Contains(c.EnarNumber)))
            .ToListAsync();

        // Statisztika a visszajelzéshez
        int sikeresDarab = 0;
        var hibásTags = inputTags.Where(tag => !matchedCattle.Any(c => c.EarTag == tag || c.EnarNumber == tag)).ToList();

        // 3. LOGIKAI SZÉTVÁLASZTÁS A TÍPUS ALAPJÁN

        // A) HA NAPI LÁBFÜRÖSZTÉS TEHENEKNEK -> NEM mentjük állatonként a Historyba!
        if (vm.TreatmentType == FootTreatmentType.Lábfürösztés)
        {
            // Itt nem gyártunk 1000 darab History sor, hanem pl. egy központi naplóba mentünk (ha van olyan táblád),
            // vagy egyszerűen csak nyugtázzuk a telepi naplóban.
            // Példaként most csak egy összesített sikeres üzenetet küldünk, vagy elmenthetsz egy darab összesített entitást.

            TempData["Success"] = $"Lábfürösztés rögzítve! {matchedCattle.Count} db állat érintett a csoportos napló alapján.";
            if (hibásTags.Any())
            {
                TempData["Warning"] = $"A következő füljelzőket nem találtam a rendszerben: {string.Join(", ", hibásTags)}";
            }
            return RedirectToAction(nameof(Index));
        }

        // B) HA KÖRMÖZÉS VAGY EGYÉB -> Itt kötelező állatonként bejegyezni az AnimalHistory-ba
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                foreach (var cattle in matchedCattle)
                {
                    // Alap kezelési rekord mentése
                    var treatment = new AnimalTreatment
                    {
                        CattleId = cattle.Id,
                        TreatmentDate = vm.TreatmentDate,
                        Category = TreatmentCategory.Láb, // A fő kategória
                        Note = $"[{vm.TreatmentType}] {vm.Note}"
                    };
                    _context.AnimalTreatments.Add(treatment);

                    // AnimalHistory bejegyzés minden egyes érintett állatnak (mivel ez féléves esemény, nem terheli túl a rendszert)
                    var history = new AnimalHistory
                    {
                        CattleId = cattle.Id,
                        EventDate = vm.TreatmentDate,
                        Type = "Gyógykezelés",
                        Comment = $"[Lábápolás - {vm.TreatmentType}] {vm.Note}".TrimEnd(' ', '-'),
                        IsEnarReported = false
                    };
                    _context.AnimalHistories.Add(history);

                    sikeresDarab++;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"{sikeresDarab} db állat esetén a(z) {vm.TreatmentType} sikeresen rögzítve lett az állatkartonokon!";

                if (hibásTags.Any())
                {
                    TempData["Warning"] = $"A következő {hibásTags.Count} db füljelzőt nem találtam az aktív állományban: {string.Join(", ", hibásTags)}";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = $"Hiba történt a tömeges mentés során: {ex.Message}";
                return View(vm);
            }
        }
    }

    // GET: Treatments/CreateSzaporodás
    public async Task<IActionResult> CreateSzaporodás()
    {
        // Csak a TEHÉN korcsoportú és AKTÍV állatokat töltjük be, ahogy kérted
        var activeCows = await _context.Cattles
            .Where(c => c.IsActive && c.AgeGroup == "Tehén")
            .OrderBy(c => c.EarTag)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.EarTag + " - " + c.EnarNumber
            })
            .ToListAsync();

        ViewBag.CowList = activeCows;

        var model = new ReproTreatmentViewModel
        {
            StartDate = DateTime.Today
        };

        return View(model);
    }

    // POST: Treatments/CreateSzaporodás
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSzaporodás(ReproTreatmentViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            // Újratöltjük a listát hiba esetén
            ViewBag.CowList = await _context.Cattles
                .Where(c => c.IsActive && c.AgeGroup == "Tehén")
                .OrderBy(c => c.EarTag)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.EarTag + " - " + c.EnarNumber })
                .ToListAsync();
            return View(vm);
        }

        // Állat ID-k meghatározása (Egyedi vs Tömeges)
        List<int> targetCattleIds = new List<int>();
        if (vm.TreatmentType == ReproTreatmentType.Tömeges_Ovsynch_Protokoll)
        {
            if (vm.SelectedCattleIds == null || !vm.SelectedCattleIds.Any())
            {
                ModelState.AddModelError("SelectedCattleIds", "Tömeges protokollhoz legalább egy tehenet ki kell választani!");
                // Újratöltés...
                ViewBag.CowList = await _context.Cattles.Where(c => c.IsActive && c.AgeGroup == "Tehén").OrderBy(c => c.EarTag).Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.EarTag + " - " + c.EnarNumber }).ToListAsync();
                return View(vm);
            }
            targetCattleIds = vm.SelectedCattleIds;
        }
        else
        {
            if (!vm.SingleCattleId.HasValue)
            {
                ModelState.AddModelError("SingleCattleId", "Egyedi kezelésnél kötelező állatot választani!");
                // Újratöltés...
                ViewBag.CowList = await _context.Cattles.Where(c => c.IsActive && c.AgeGroup == "Tehén").OrderBy(c => c.EarTag).Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.EarTag + " - " + c.EnarNumber }).ToListAsync();
                return View(vm);
            }
            targetCattleIds.Add(vm.SingleCattleId.Value);
        }

        // Gyógyszerek kikeresése a névről (ha nincsenek meg, a rendszer nem akad el, csak a szövegbe írja)
        var gonavetMed = await _context.Medications.FirstOrDefaultAsync(m => m.Name.Contains("Gonavet"));
        var pgfMed = await _context.Medications.FirstOrDefaultAsync(m => m.Name.Contains("PGF-forte") || m.Name.Contains("PGF"));

        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                foreach (var cattleId in targetCattleIds)
                {
                    if (vm.TreatmentType == ReproTreatmentType.Tömeges_Ovsynch_Protokoll)
                    {
                        // --- 4 LÉPCSŐS OVSYNCH PROTOKOLL AUTOMATIKUS GENERÁLÁSA ---

                        // 1. lépés: 0. nap - Gonavet injekció
                        DateTime d1 = vm.StartDate;
                        _context.AnimalTreatments.Add(new AnimalTreatment
                        {
                            CattleId = cattleId,
                            TreatmentDate = d1,
                            Category = TreatmentCategory.Szaporodás,
                            MedicationId = gonavetMed?.Id ?? 0, // JAVÍTVA: ha null, akkor 0 (vagy egy létező alapértelmezett ID)
                            AdministeredDose = 2,
                            Note = "[Ovsynch - 1. lépés] Gonavet injekció (2ml)"
                        });
                        _context.AnimalHistories.Add(new AnimalHistory { CattleId = cattleId, EventDate = d1, Type = "Gyógykezelés", Comment = "[Ovsynch protokoll elindítva] 1. Gonavet injekció beadva.", IsEnarReported = false });
                        if (gonavetMed != null && gonavetMed.Quantity >= 2) gonavetMed.Quantity -= 2;

                        // 2. lépés: +7 nap - PGF-forte injekció
                        DateTime d2 = vm.StartDate.AddDays(7);
                        _context.AnimalTreatments.Add(new AnimalTreatment
                        {
                            CattleId = cattleId,
                            TreatmentDate = d2,
                            Category = TreatmentCategory.Szaporodás,
                            MedicationId = pgfMed?.Id ?? 0, // JAVÍTVA
                            AdministeredDose = 2,
                            Note = "[Ovsynch - 2. lépés] PGF-forte injekció (2ml)"
                        });
                        _context.AnimalHistories.Add(new AnimalHistory { CattleId = cattleId, EventDate = d2, Type = "Gyógykezelés", Comment = "[Ovsynch protokoll] 2. PGF-forte injekció ütemezve/beadva.", IsEnarReported = false });
                        if (pgfMed != null && pgfMed.Quantity >= 2) pgfMed.Quantity -= 2;

                        // 3. lépés: +8 nap (+1 nap az előzőhöz) - PGF-forte injekció
                        DateTime d3 = vm.StartDate.AddDays(8);
                        _context.AnimalTreatments.Add(new AnimalTreatment
                        {
                            CattleId = cattleId,
                            TreatmentDate = d3,
                            Category = TreatmentCategory.Szaporodás,
                            MedicationId = pgfMed?.Id ?? 0, // JAVÍTVA
                            AdministeredDose = 2,
                            Note = "[Ovsynch - 3. lépés] PGF-forte másodolás (2ml)"
                        });
                        _context.AnimalHistories.Add(new AnimalHistory { CattleId = cattleId, EventDate = d3, Type = "Gyógykezelés", Comment = "[Ovsynch protokoll] 3. PGF-forte ismétlés ütemezve/beadva.", IsEnarReported = false });
                        if (pgfMed != null && pgfMed.Quantity >= 2) pgfMed.Quantity -= 2;

                        // 4. lépés: +9 nap (+1 nap az előzőhöz) - Gonavet injekció
                        DateTime d4 = vm.StartDate.AddDays(9);
                        _context.AnimalTreatments.Add(new AnimalTreatment
                        {
                            CattleId = cattleId,
                            TreatmentDate = d4,
                            Category = TreatmentCategory.Szaporodás,
                            MedicationId = gonavetMed?.Id ?? 0, // JAVÍTVA
                            AdministeredDose = 2,
                            Note = "[Ovsynch - 4. lépés] Gonavet záró injekció (2ml)"
                        });
                        _context.AnimalHistories.Add(new AnimalHistory { CattleId = cattleId, EventDate = d4, Type = "Gyógykezelés", Comment = "[Ovsynch protokoll] 4. Záró Gonavet injekció ütemezve/beadva. Várható termékenyítés: " + d4.AddDays(1).ToShortDateString(), IsEnarReported = false });
                        if (gonavetMed != null && gonavetMed.Quantity >= 2) gonavetMed.Quantity -= 2;
                    }
                    else
                    {
                        // --- EGYEDI KEZELÉS ---
                        var treatment = new AnimalTreatment
                        {
                            CattleId = cattleId,
                            TreatmentDate = vm.StartDate,
                            Category = TreatmentCategory.Szaporodás,
                            Note = $"[{vm.TreatmentType}] {vm.Note}"
                        };
                        _context.AnimalTreatments.Add(treatment);

                        var history = new AnimalHistory
                        {
                            CattleId = cattleId,
                            EventDate = vm.StartDate,
                            Type = "Gyógykezelés",
                            Comment = $"[Szaporodásbiológia - {vm.TreatmentType.ToString().Replace("_", " ")}] {vm.Note}",
                            IsEnarReported = false
                        };
                        _context.AnimalHistories.Add(history);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = vm.TreatmentType == ReproTreatmentType.Tömeges_Ovsynch_Protokoll
                    ? $"Az Ovsynch szinkronizációs protokoll sikeresen rögzítve {targetCattleIds.Count} tehénre (összesen {targetCattleIds.Count * 4} kezelési bejegyzés ütemezve)!"
                    : "Az egyedi szaporodásbiológiai kezelés sikeresen rögzítve!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = $"Hiba történt a mentés során: {ex.Message}";

                // Újratöltés hiba esetén...
                ViewBag.CowList = await _context.Cattles.Where(c => c.IsActive && c.AgeGroup == "Tehén").OrderBy(c => c.EarTag).Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.EarTag + " - " + c.EnarNumber }).ToListAsync();
                return View(vm);
            }
        }
    }

    // GET: Treatments/CreateEgyéb
    public async Task<IActionResult> CreateEgyéb()
    {
        // Összes aktív állat (Tehén, Üsző, Növendék jöhet)
        var activeCattle = await _context.Cattles
            .Where(c => c.IsActive)
            .OrderBy(c => c.EarTag)
            .Select(c => new { c.Id, DisplayName = c.EarTag + " - " + c.EnarNumber })
            .ToListAsync();

        ViewBag.CattleId = new SelectList(activeCattle, "Id", "DisplayName");

        // Betöltjük a legördülő listákat (Gyógyszerek, Támogató szerek az Egyéb kategóriából)
        PopulateDropLists(TreatmentCategory.Egyéb);

        var model = new OtherTreatmentViewModel
        {
            TreatmentDate = DateTime.Today
        };

        return View(model);
    }

    // POST: Treatments/CreateEgyéb
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEgyéb(OtherTreatmentViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            // Újratöltés hiba esetén
            var activeCattle = await _context.Cattles.Where(c => c.IsActive).OrderBy(c => c.EarTag).Select(c => new { c.Id, DisplayName = c.EarTag + " - " + c.EnarNumber }).ToListAsync();
            ViewBag.CattleId = new SelectList(activeCattle, "Id", "DisplayName");
            PopulateDropLists(TreatmentCategory.Egyéb);
            return View(vm);
        }

        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // 1. Elsődleges gyógyszer kezelése és levonása
                var med1 = await _context.Medications.FindAsync(vm.MedicationId);
                if (med1 == null) return NotFound("Az elsődleges gyógyszer nem található.");

                if (med1.Quantity < vm.AdministeredDose)
                {
                    ModelState.AddModelError("AdministeredDose", $"Nincs elég készlet a(z) {med1.Name} szerből! Elérhető: {med1.Quantity} {med1.Unit}");
                    throw new Exception("Készlethiány.");
                }
                med1.Quantity -= vm.AdministeredDose;

                // ÉVI számítás az 1. szerre
                DateTime? milkExpiry = med1.WithdrawalPeriodMilk > 0 ? vm.TreatmentDate.AddDays(med1.WithdrawalPeriodMilk) : null;
                DateTime? meatExpiry = med1.WithdrawalPeriodMeat > 0 ? vm.TreatmentDate.AddDays(med1.WithdrawalPeriodMeat) : null;

                // 2. Másodlagos gyógyszer kezelése (ha van)
                string secondaryText = "";
                if (vm.SecondaryMedicationId.HasValue && vm.SecondaryAdministeredDose.HasValue)
                {
                    var med2 = await _context.Medications.FindAsync(vm.SecondaryMedicationId.Value);
                    if (med2 != null)
                    {
                        if (med2.Quantity < vm.SecondaryAdministeredDose.Value)
                        {
                            ModelState.AddModelError("SecondaryAdministeredDose", $"Nincs elég készlet a másodlagos {med2.Name} szerből! Elérhető: {med2.Quantity} {med2.Unit}");
                            throw new Exception("Készlethiány a másodlagos szerből.");
                        }
                        med2.Quantity -= vm.SecondaryAdministeredDose.Value;
                        secondaryText = $" + {med2.Name} ({vm.SecondaryAdministeredDose.Value} {med2.Unit})";

                        // Ha a másodlagos szer ÉVI-je szigorúbb, azt vesszük figyelembe
                        if (med2.WithdrawalPeriodMilk > 0)
                        {
                            DateTime med2Milk = vm.TreatmentDate.AddDays(med2.WithdrawalPeriodMilk);
                            if (milkExpiry == null || med2Milk > milkExpiry) milkExpiry = med2Milk;
                        }
                        if (med2.WithdrawalPeriodMeat > 0)
                        {
                            DateTime med2Meat = vm.TreatmentDate.AddDays(med2.WithdrawalPeriodMeat);
                            if (meatExpiry == null || med2Meat > meatExpiry) meatExpiry = med2Meat;
                        }
                    }
                }

                // 3. AnimalTreatment rekord mentése
                var treatment = new AnimalTreatment
                {
                    CattleId = vm.CattleId,
                    TreatmentDate = vm.TreatmentDate,
                    Category = TreatmentCategory.Egyéb,
                    MedicationId = vm.MedicationId,
                    AdministeredDose = vm.AdministeredDose,
                    MilkWithdrawalExpiry = milkExpiry,
                    MeatWithdrawalExpiry = meatExpiry,
                    Note = $"[{vm.TreatmentType.ToString().Replace("_", " ")}] {med1.Name} ({vm.AdministeredDose} {med1.Unit}){secondaryText}. {vm.Note}"
                };
                _context.AnimalTreatments.Add(treatment);

                // 4. AnimalHistory mentése
                var history = new AnimalHistory
                {
                    CattleId = vm.CattleId,
                    EventDate = vm.TreatmentDate,
                    Type = "Gyógykezelés",
                    Comment = $"[Belgyógyászat - {vm.TreatmentType.ToString().Replace("_", " ")}] Kezelve: {med1.Name}{secondaryText}. Tej ÉVI: {milkExpiry?.ToShortDateString() ?? "Nincs"}, Hús ÉVI: {meatExpiry?.ToShortDateString() ?? "Nincs"}.",
                    IsEnarReported = false
                };
                _context.AnimalHistories.Add(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Az egyedi kezelés sikeresen rögzítve, a raktárkészlet levonva!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                if (ModelState.IsValid) // Ha nem validációs hiba miatt szállt el (pl belső hiba)
                {
                    TempData["Error"] = $"Hiba történt a mentés során: {ex.Message}";
                }

                // Újratöltés hibaágon
                var activeCattle = await _context.Cattles.Where(c => c.IsActive).OrderBy(c => c.EarTag).Select(c => new { c.Id, DisplayName = c.EarTag + " - " + c.EnarNumber }).ToListAsync();
                ViewBag.CattleId = new SelectList(activeCattle, "Id", "DisplayName");
                PopulateDropLists(TreatmentCategory.Egyéb);
                return View(vm);
            }
        }
    }

    private void PopulateDropLists(TreatmentCategory category)
    {
        // Ahol korábban string szűrés volt (pl. m.Category == category.ToString())
        // Ott most már közvetlenül az Enummal tudsz szűrni:
        ViewBag.Medications = _context.Medications
            .Where(m => m.Category == category && m.Quantity > 0)
            .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Name })
            .ToList();

        // A tehenek listája
        ViewBag.CattleId = _context.Cattles
            .Where(c => c.IsActive)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.EarTag + " - " + c.EnarNumber })
            .ToList();

        // Kombinációs gyógyszerek (pl. az Egyéb kezelésből az Eurofit/Meloxicam gyulladáscsökkentők)
        ViewBag.SecondaryMedications = _context.Medications
            .Where(m => m.Category == TreatmentCategory.Egyéb && m.Quantity > 0)
            .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Name })
            .ToList();
    }
}