using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO.Compression;
using System.Xml;

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
            _context.AnimalHistories.Add(new AnimalHistory
            {
                CattleId = cattle.Id,
                EventDate = DateTime.Now,
                Weight = cattle.CurrentWeight,
                WeightGain = 0,
                Type = "Kézi rögzítés / Kezdő súly"
            });
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
            string changeWeight,     // bool helyett string
            string changeAgeGroup,   // bool helyett string
            string changeLocation,   // bool helyett string
            string newAgeGroup,
            int? newHerdId,
            string stallName)
        {
            if (selectedCattleIds == null || selectedCattleIds.Length == 0) return RedirectToAction(nameof(Movement));

            // Manuális konverzió bool-ra (mivel a checkbox "on" értéket küld, ha be van pipálva)
            bool isWeightChange = changeWeight == "on" || changeWeight == "true";
            bool isAgeChange = changeAgeGroup == "on" || changeAgeGroup == "true";
            bool isLocChange = changeLocation == "on" || changeLocation == "true";

            var ageGroups = new List<string> {
                "Itatásos borjú", "Borjú", "Növendék 6-9", "Növendék 9-12",
                "Növendék 12 hó-tól", "Vemhes üsző", "Tehén"
            };

            var cattleList = await _context.Cattles.Where(c => selectedCattleIds.Contains(c.Id)).ToListAsync();

            // Számlálók a visszajelzéshez
            int successCount = 0;
            int errorCount = 0;

            foreach (var cattle in cattleList)
            {
                // Elmentjük az eredeti állapotot a naplózáshoz
                string oldAgeGroup = cattle.AgeGroup;
                int? oldHerdId = cattle.CurrentHerdId;
                string oldStall = cattle.Stall;

                var history = new AnimalHistory
                {
                    CattleId = cattle.Id,
                    EventDate = moveDate,
                    OldAgeGroup = oldAgeGroup,
                    OldHerdId = oldHerdId,
                    Weight = cattle.CurrentWeight,
                    WeightGain = 0,
                    StallName = oldStall,
                    Type = ""
                };

                // 1. KORCSOPORT VÁLTÁS
                if (isAgeChange)
                {
                    int currentIndex = ageGroups.IndexOf(oldAgeGroup);
                    int nextIndex = ageGroups.IndexOf(newAgeGroup);

                    if (nextIndex != currentIndex && nextIndex != currentIndex + 1)
                    {
                        TempData["Error"] = $"Hiba: {cattle.EarTag} nem ugorhat {oldAgeGroup} csoportból {newAgeGroup} csoportba!";
                        errorCount++;
                        continue; // Itt ugrik a következő állatra, nem növeli a successCount-ot
                    }

                    cattle.AgeGroup = newAgeGroup;
                    history.NewAgeGroup = newAgeGroup;
                    history.Type += "Korosbítás ";
                }

                // 2. SÚLY KEZELÉSE
                if (isWeightChange)
                {
                    // Ha még sosem volt mérve, de van születési súlya, induljunk onnan
                    if (cattle.CurrentWeight <= 0 && cattle.BirthWeight > 0)
                        cattle.CurrentWeight = cattle.BirthWeight;

                    double oldWeight = cattle.CurrentWeight;

                    if (weightMode == "fixed" && commonWeightValue.HasValue)
                        cattle.CurrentWeight = commonWeightValue.Value;
                    else if (weightMode == "gain" && commonWeightValue.HasValue)
                        cattle.CurrentWeight += commonWeightValue.Value;
                    else if (weightMode == "individual" && individualWeights != null && individualWeights.ContainsKey(cattle.Id))
                        cattle.CurrentWeight = individualWeights[cattle.Id];

                    // Frissítjük a history-t a tényleges új adatokkal
                    history.Weight = cattle.CurrentWeight;
                    history.WeightGain = cattle.CurrentWeight - oldWeight;

                    if (!isAgeChange && !isLocChange) history.Type = "Súlymérés";
                }
                else
                {
                    // HA NINCS SÚLYMÉRÉS: 
                    // A history.Weight már az alaphelyzetben megkapta a cattle.CurrentWeight-et,
                    // a cattle.CurrentWeight-hez pedig hozzá sem nyúlunk, így marad a régi.
                }
                // 3. HELYVÁLTOZTATÁS
                // ISTÁLLÓ: Ha van megadva új név, átírjuk. Ha nincs, marad a régi (nem töröljük!)
                if (!string.IsNullOrEmpty(stallName))
                {
                    cattle.Stall = stallName;
                    history.StallName = stallName;

                    // Ha nem volt tenyészetváltás, csak istálló, akkor is jelezzük
                    if (!isLocChange) history.Type += "Istálló váltás ";
                }
                else if (string.IsNullOrEmpty(cattle.Stall))
                {
                    // Ha valamiért tök üres lenne (pl. importált adat), legyen egy alapértelmezés
                    cattle.Stall = "Borjúkert";
                    history.StallName = "Borjúkert";
                }
                // TENYÉSZET (Csak ha be van pipálva a Tenyészetváltás)
                if (isLocChange)
                {
                    if (newHerdId.HasValue && cattle.CurrentHerdId != newHerdId)
                    {
                        history.NewHerdId = newHerdId;
                        cattle.CurrentHerdId = newHerdId.Value;
                        cattle.RequiresEnar5147 = true;
                        history.Type += "Áthelyezés (Tenyészetváltás) ";
                    }
                }

                history.NewAgeGroup ??= cattle.AgeGroup;
                history.NewHerdId ??= cattle.CurrentHerdId;

                if (string.IsNullOrEmpty(history.Type)) history.Type = "Adatmódosítás";
                successCount++;
                _context.Update(cattle); // Biztosítjuk a frissítést
                _context.AnimalHistories.Add(history);
            }

            if (successCount > 0)
            {
                await _context.SaveChangesAsync();
                // Csak akkor írjuk ki a sikert, ha nem volt hiba, vagy ha legalább egy állat sikerült
                if (errorCount == 0)
                {
                    TempData["Success"] = $"{successCount} állat adatai sikeresen frissítve.";
                }
                else
                {
                    TempData["Success"] = $"{successCount} állat frissítve, de {errorCount} állatnál hiba történt.";
                }
            }
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
        // Megjelenítjük a várakozó áthelyezéseket
        public async Task<IActionResult> PendingMovements()
        {
            var pending = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .Where(c => c.RequiresEnar5147 && c.IsActive)
                .ToListAsync();

            // Készítünk egy szótárat a régi tenyészetkódokhoz, hogy a View-ban ne kelljen SQL-ezni
            var sourceHerds = new Dictionary<int, string>();
            foreach (var c in pending)
            {
                var lastMove = await _context.AnimalHistories
                    .Where(h => h.CattleId == c.Id && h.NewHerdId == c.CurrentHerdId)
                    .OrderByDescending(h => h.EventDate)
                    .FirstOrDefaultAsync();

                if (lastMove?.OldHerdId != null)
                {
                    var oldHerd = await _context.Herds.FindAsync(lastMove.OldHerdId);
                    sourceHerds[c.Id] = oldHerd?.HerdCode ?? "467355";
                }
                else
                {
                    sourceHerds[c.Id] = "467355"; // Alapértelmezett
                }
            }
            ViewBag.SourceHerds = sourceHerds;

            return View(pending);
        }

        // Tenyészetváltáshoz XML Generálása
        [HttpPost]
        public async Task<IActionResult> GenerateEnar5147Package(int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0)
                return Content("<script>alert('Nincs kijelölt tétel!'); window.history.back();</script>", "text/html");

            var cattleToReport = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .Where(c => selectedIds.Contains(c.Id))
                .ToListAsync();

            QuestPDF.Settings.License = LicenseType.Community;

            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    var groupedByTarget = cattleToReport.GroupBy(c => c.CurrentHerd?.HerdCode ?? "ISMERETLEN");

                    foreach (var group in groupedByTarget)
                    {
                        string targetHerdCode = group.Key;
                        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

                        XNamespace ns2 = "http://e5147.client.enar.si.hu";
                        var root = new XElement(ns2 + "SzmarhaBejelentok", new XAttribute(XNamespace.Xmlns + "ns2", ns2.NamespaceName));

                        var listForPdf = new List<Enar5147PdfModel>();
                        int sorszam = 1;

                        foreach (var cattle in group)
                        {
                            // Itt keressük meg a mozgást
                            var lastHistory = await _context.AnimalHistories
                               .Where(h => h.CattleId == cattle.Id &&
                                h.NewHerdId == cattle.CurrentHerdId &&
                                h.Type == "Áthelyezés (Tenyészetváltás)")
                                .OrderByDescending(h => h.EventDate)
                                .FirstOrDefaultAsync();

                            // --- ÚJ RÉSZ: Státusz frissítése az archívumhoz ---
                            if (lastHistory != null)
                            {
                                lastHistory.IsEnarReported = true; // Beállítjuk, hogy a havi naplóban zöld legyen
                                _context.Entry(lastHistory).State = EntityState.Modified;
                            }
                            // ------------------------------------------------

                            string sourceHerdCode = "467355";
                            if (lastHistory?.OldHerdId != null)
                            {
                                var oldHerd = await _context.Herds.FindAsync(lastHistory.OldHerdId);
                                sourceHerdCode = oldHerd?.HerdCode ?? "467355";
                            }

                            string enarRaw = cattle.EnarNumber ?? "";
                            string enarOnly = enarRaw.StartsWith("HU") ? enarRaw.Substring(2).Trim() : enarRaw.Trim();

                            root.Add(new XElement("SzmarhaBejelento",
                                new XElement("Sorszam", sorszam++),
                                new XElement("AzonositoOrszagkodja", "HU"),
                                new XElement("Azonosito", enarOnly),
                                new XElement("KiadasiSorszam", cattle.PassportSequence.ToString("D2")),
                                new XElement("TenyeszetKodIndito", sourceHerdCode),
                                new XElement("KikerulesDatuma", lastHistory?.EventDate.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd")),
                                new XElement("TenyeszetKodFogado", targetHerdCode),
                                new XElement("ErkezesDatuma", lastHistory?.EventDate.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd")),
                                new XElement("SurgosseggelKezelendo", "2"),
                                new XElement("MarhalevelTipusa", "2")
                            ));

                            listForPdf.Add(new Enar5147PdfModel
                            {
                                Sorszam = sorszam - 1,
                                Enar = "HU " + enarOnly,
                                SourceHerd = sourceHerdCode,
                                TargetHerd = targetHerdCode,
                                Date = lastHistory?.EventDate ?? DateTime.Now,
                                PassportSeq = cattle.PassportSequence.ToString("D2")
                            });

                            cattle.PassportSequence += 1;
                            cattle.PassportNumber = "Kérve";
                            cattle.RequiresEnar5147 = false;
                        }

                        var settings = new XmlWriterSettings { Indent = false, Encoding = new UTF8Encoding(false) };
                        string xmlString;
                        using (var xmlMs = new MemoryStream())
                        {
                            using (var writer = XmlWriter.Create(xmlMs, settings)) { root.WriteTo(writer); }
                            xmlString = Encoding.UTF8.GetString(xmlMs.ToArray())
                                .Replace("\"", "'")
                                .Replace("utf-8", "UTF-8");
                        }

                        var xmlEntry = archive.CreateEntry($"atrh_{targetHerdCode}_{timestamp}.xml");
                        using (var entryWriter = new StreamWriter(xmlEntry.Open(), new UTF8Encoding(false)))
                        {
                            entryWriter.Write(xmlString);
                        }

                        var pdfDoc = CreateEnar5147Pdf(listForPdf, targetHerdCode);
                        var pdfEntry = archive.CreateEntry($"atrh_lista_{targetHerdCode}_{DateTime.Now:yyyyMMdd}.pdf");
                        using (var entryStream = pdfEntry.Open())
                        {
                            byte[] pdfBytes = pdfDoc.GeneratePdf();
                            entryStream.Write(pdfBytes, 0, pdfBytes.Length);
                        }
                    }
                }
                await _context.SaveChangesAsync();
                return File(memoryStream.ToArray(), "application/zip", $"ENAR_5147_{DateTime.Now:yyyyMMdd}.zip");
            }
        }

        // Segédmodell a PDF-hez
        public class Enar5147PdfModel
        {
            public int Sorszam { get; set; }
            public string Enar { get; set; }
            public string SourceHerd { get; set; }
            public string TargetHerd { get; set; }
            public DateTime Date { get; set; }
            public string PassportSeq { get; set; }
        }

        private IDocument CreateEnar5147Pdf(List<Enar5147PdfModel> items, string targetHerdCode)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Verdana));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("ÁTKÖTÉSI ELLENŐRZŐ LISTA (5147)").FontSize(14).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"Fogadó tenyészet: {targetHerdCode}").FontSize(10);
                        });
                        row.RelativeItem().AlignRight().Text($"{DateTime.Now:yyyy.MM.dd HH:mm}").FontSize(9);
                    });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(25);  // Ssz
                            columns.RelativeColumn(2.5f);// ENAR
                            columns.RelativeColumn(2);   // Régi teny.
                            columns.RelativeColumn(2);   // Új teny.
                            columns.RelativeColumn(1.5f);// Marhalevél
                            columns.RelativeColumn(2);   // Dátum
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(PdfHeaderStyle).Text("Ssz.");
                            header.Cell().Element(PdfHeaderStyle).Text("Állat ENAR");
                            header.Cell().Element(PdfHeaderStyle).Text("Régi Teny.");
                            header.Cell().Element(PdfHeaderStyle).Text("Új Teny.");
                            header.Cell().Element(PdfHeaderStyle).Text("Melléklet");
                            header.Cell().Element(PdfHeaderStyle).Text("Dátum");
                        });

                        foreach (var item in items)
                        {
                            table.Cell().Element(PdfRowStyle).Text(item.Sorszam.ToString());
                            table.Cell().Element(PdfRowStyle).Text(item.Enar);
                            table.Cell().Element(PdfRowStyle).Text(item.SourceHerd);
                            table.Cell().Element(PdfRowStyle).Text(item.TargetHerd);
                            table.Cell().Element(PdfRowStyle).Text(item.PassportSeq);
                            table.Cell().Element(PdfRowStyle).Text(item.Date.ToString("yyyy.MM.dd"));
                        }
                    });
                });
            });
        }
        // Segédstílusok (ugyanaz mint a CattleControllerben)
        private IContainer PdfHeaderStyle(IContainer container)
        {
            return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
        }

        private IContainer PdfRowStyle(IContainer container)
        {
            return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten3);
        }
    }
}
