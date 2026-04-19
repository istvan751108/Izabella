using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Izabella.Models;

namespace Izabella.Controllers
{
    public class CattlesController : Controller
    {
        private readonly IzabellaDbContext _context;

        public CattlesController(IzabellaDbContext context)
        {
            _context = context;
        }

        // GET: Cattles
        public async Task<IActionResult> Index(string searchGroup, string searchTag)
        {
            // 1. Alap lekérdezés: CSAK AZ AKTÍVAK (IsActive == true)
            var query = _context.Cattles
                .Include(c => c.CurrentHerd)
                .Include(c => c.Company)
                .Where(c => c.IsActive == true)
                .AsQueryable();

            // 2. Szűrés korcsoportra
            if (!string.IsNullOrEmpty(searchGroup))
            {
                query = query.Where(c => c.AgeGroup == searchGroup);
            }

            // 3. Keresés fülszám alapján (kis/nagybetű függetlenül érdemes)
            if (!string.IsNullOrEmpty(searchTag))
            {
                query = query.Where(c => c.EarTag.Contains(searchTag.ToUpper()));
            }

            // 4. Korcsoportok listája - CSAK AZ AKTÍV ÁLLOMÁNYBÓL
            // Így nem fogsz látni "Halva született" opciót az élő állatok listájában
            ViewBag.AgeGroups = await _context.Cattles
                .Where(c => c.IsActive == true && c.AgeGroup != null)
                .Select(c => c.AgeGroup)
                .Distinct()
                .OrderBy(g => g)
                .ToListAsync();

            return View(await query.OrderByDescending(c => c.BirthDate).ToListAsync());
        }

        // GET: Cattles/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cattle = await _context.Cattles
                .Include(c => c.Company)
                .Include(c => c.CurrentHerd)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (cattle == null)
            {
                return NotFound();
            }

            return View(cattle);
        }

        // GET: Cattles/Create
        public IActionResult Create()
        {
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name");
            ViewData["CurrentHerdId"] = new SelectList(_context.Herds, "Id", "HerdCode");
            PopulateBreeds();
            return View();
        }

        // POST: Cattles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Cattle cattle)
        {
            // Kényszerítsük ki az érvényességet azokra a mezőkre, amik üresek maradhatnak
            ModelState.Remove("CurrentHerd");
            ModelState.Remove("Company");

            if (ModelState.IsValid)
            {
                _context.Add(cattle);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Ha hiba van, újraépítjük a listákat a nézethez
            ViewBag.CompanyId = new SelectList(_context.Companies, "Id", "Name", cattle.CompanyId);
            ViewBag.CurrentHerdId = new SelectList(_context.Herds, "Id", "Name", cattle.CurrentHerdId);
            PopulateBreeds();
            return View(cattle);
        }

        // GET: Cattles/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cattle = await _context.Cattles.FindAsync(id);
            if (cattle == null)
            {
                return NotFound();
            }
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name", cattle.CompanyId);
            ViewData["CurrentHerdId"] = new SelectList(_context.Herds, "Id", "HerdCode", cattle.CurrentHerdId);
            PopulateBreeds(cattle.BreedCode);
            return View(cattle);
        }

        // POST: Cattles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EarTag,EnarNumber,PassportNumber,PassportSequence,CompanyId,CurrentHerdId,AgeGroup,IsTwin,IsAlive,DamAgeAtCalving,BirthDate,BirthWeight,Gender,MotherEnar,FatherKlsz,ExitDate,ExitType,IsActive,BreedCode")] Cattle cattle, string returnUrl = null)
        {
            if (id != cattle.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cattle);
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(returnUrl)) return LocalRedirect(returnUrl);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    // Ha az SQL dob hibát (pl. truncation), itt elkapjuk
                    if (ex.InnerException?.Message.Contains("truncated") == true)
                    {
                        ModelState.AddModelError("PassportNumber", "Túl hosszú adatot adtál meg! Kérlek rövidítsd le.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Adatbázis mentési hiba történt: " + ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Váratlan hiba: " + ex.Message);
                }
            }

            // Ha idáig eljutunk, hiba volt, újraépítjük a listákat a nézethez
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name", cattle.CompanyId);
            ViewData["CurrentHerdId"] = new SelectList(_context.Herds, "Id", "HerdCode", cattle.CurrentHerdId);
            PopulateBreeds(cattle.BreedCode);
            return View(cattle);
        }

        // GET: Cattles/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cattle = await _context.Cattles
                .Include(c => c.Company)
                .Include(c => c.CurrentHerd)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (cattle == null)
            {
                return NotFound();
            }

            return View(cattle);
        }

        // POST: Cattles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cattle = await _context.Cattles.FindAsync(id);
            if (cattle != null)
            {
                _context.Cattles.Remove(cattle);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CattleExists(int id)
        {
            return _context.Cattles.Any(e => e.Id == id);
        }
        private void PopulateBreeds(int? selectedBreed = 22)
        {
            var breeds = new List<SelectListItem>
            {
                new SelectListItem { Value = "22", Text = "Holstein-fríz (22)" },
                new SelectListItem { Value = "1", Text = "Magyartarka (1)" },
                new SelectListItem { Value = "12", Text = "Jersey (12)" },
                new SelectListItem { Value = "13", Text = "Európai barna (13)" },
                new SelectListItem { Value = "19", Text = "Mokány (19)" },
                new SelectListItem { Value = "20", Text = "Erdélyi borzderes (20)" },
                new SelectListItem { Value = "33", Text = "Kárpáti borzderes (33)" },
                new SelectListItem { Value = "63", Text = "Montbeliarde (63)" },
                new SelectListItem { Value = "88", Text = "Brown Swiss (88)" },
                new SelectListItem { Value = "99", Text = "Egyéb tejhasznú (99)" }
            };

            ViewBag.Breeds = new SelectList(breeds, "Value", "Text", selectedBreed);
        }
        [HttpPost]
        public async Task<IActionResult> RecordDeath(int id, DateTime exitDate, string receiptNumber)
        {
            var cattle = await _context.Cattles.FindAsync(id);
            if (cattle == null) return NotFound();

            cattle.IsActive = false;
            cattle.IsAlive = false;
            cattle.ExitDate = exitDate;
            cattle.ExitType = (ExitType)SaleType.Slaughter;

            var deathTransaction = new SaleTransaction
            {
                CattleId = cattle.Id,
                CustomerId = 2,
                SaleDate = exitDate,
                Type = SaleType.Slaughter,
                // Ha nincs megadva, generáljunk egy "ELH-" kezdetű számot
                ReceiptNumber = !string.IsNullOrEmpty(receiptNumber) ? receiptNumber : "ELH-" + cattle.EarTag + "-" + exitDate.ToString("yyyyMMdd"),
                GrossWeight = 0,
                NetWeight = 0,
                UnitPrice = 0,
                TotalNetPrice = 0,
                DeductionPercentage = 0,
                IsReported = false
            };

            _context.SaleTransactions.Add(deathTransaction);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Elhullás rögzítve: {cattle.EarTag}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UndoBirthReport(int id)
        {
            var cattle = await _context.Cattles.FindAsync(id);
            if (cattle != null)
            {
                // Visszaállítjuk "Nincs" állapotra, így újra megjelenik az ENAR bejelentő oldalon
                cattle.PassportNumber = "Nincs";
                await _context.SaveChangesAsync();
                TempData["Success"] = $"A(z) {cattle.EarTag} fülszámú állat bejelentése sikeresen visszavonva.";
            }
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> ProcessMovement(
            int[] selectedCattleIds,
            DateTime moveDate,
            string weightMode,
            double? commonWeightValue,
            Dictionary<int, double> individualWeights,
            bool changeAgeGroup, string newAgeGroup,
            bool changeLocation, int? newHerdId, string stallName)
                {
                    if (selectedCattleIds == null || selectedCattleIds.Length == 0) return RedirectToAction(nameof(Movement));

                    // Korcsoport sorrend meghatározása
                    var ageGroups = new List<string> {
                "Itatásos borjú", "Borjú", "Növendék 6-9", "Növendék 9-12",
                "Növendék 12 hó-tól", "Vemhes üsző", "Tehén"
            };

            var cattleList = await _context.Cattles.Where(c => selectedCattleIds.Contains(c.Id)).ToListAsync();

            foreach (var cattle in cattleList)
            {
                // KORCSOPORT ELLENŐRZÉSE
                if (changeAgeGroup)
                {
                    int currentIndex = ageGroups.IndexOf(cattle.AgeGroup);
                    int nextIndex = ageGroups.IndexOf(newAgeGroup);

                    // Csak akkor engedjük, ha ugyanaz marad (súlymérés miatt) vagy pontosan a következő
                    if (nextIndex != currentIndex && nextIndex != currentIndex + 1)
                    {
                        TempData["Error"] = $"Hiba: {cattle.EarTag} nem ugorhat {cattle.AgeGroup} csoportból {newAgeGroup} csoportba!";
                        continue;
                    }
                }

                var history = new AnimalHistory
                {
                    CattleId = cattle.Id,
                    EventDate = moveDate,
                    OldAgeGroup = cattle.AgeGroup,
                    OldHerdId = cattle.CurrentHerdId,
                    Type = ""
                };

                // Súly kezelése
                double oldWeight = cattle.CurrentWeight;
                if (weightMode == "fixed") cattle.CurrentWeight = commonWeightValue ?? cattle.CurrentWeight;
                else if (weightMode == "gain") cattle.CurrentWeight += commonWeightValue ?? 0;
                else if (weightMode == "individual" && individualWeights.ContainsKey(cattle.Id))
                    cattle.CurrentWeight = individualWeights[cattle.Id];

                history.Weight = cattle.CurrentWeight;
                history.WeightGain = cattle.CurrentWeight - oldWeight;

                // Korcsoport frissítése
                if (changeAgeGroup)
                {
                    cattle.AgeGroup = newAgeGroup;
                    history.NewAgeGroup = newAgeGroup;
                    history.Type += "Korosbítás ";
                }

                // Helyváltoztatás
                if (changeLocation)
                {
                    if (newHerdId.HasValue && cattle.CurrentHerdId != newHerdId)
                    {
                        history.NewHerdId = newHerdId;
                        cattle.CurrentHerdId = newHerdId.Value;
                        cattle.RequiresEnar5147 = true; // Ez jelzi, hogy XML kell!
                    }
                    cattle.Stall = stallName; // Itt mentjük el az istállót (pl. "Ketrec" vagy "12")
                    history.StallName = stallName;
                    history.Type += "Áthelyezés";
                }

                if (string.IsNullOrEmpty(history.Type)) history.Type = "Súlymérés";
                _context.AnimalHistories.Add(history);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Műveletek sikeresen rögzítve!";
            return RedirectToAction(nameof(Movement));
        }
        // CattlesController.cs
        public async Task<IActionResult> Movement()
        {
            // Lekérjük az összes aktív állatot
            var model = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .Where(c => c.IsActive)
                .ToListAsync();

            // Szükség van a tenyészetekre a lenyíló listához
            ViewBag.Herds = await _context.Herds.ToListAsync();
            ViewBag.ExistingStalls = await _context.Cattles
                .Where(c => !string.IsNullOrEmpty(c.Stall))
                .Select(c => c.Stall)
                .Distinct()
                .ToListAsync();
            return View(model);
        }
    }
    }
