using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer; // Opcionális

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
            var birthHerds = _context.Herds
                .Where(h => !string.IsNullOrEmpty(h.DefaultPrefix))
                .Select(h => new { h.Id, Name = h.Name + " (" + h.HerdCode + ")" })
                .ToList();

            // Fajtalista összeállítása (a kép alapján)
            var breeds = new List<SelectListItem>
            {
                new SelectListItem { Value = "22", Text = "Holstein-fríz (22)", Selected = true },
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

            ViewBag.Herds = new SelectList(birthHerds, "Id", "Name");
            ViewBag.Breeds = breeds; // Ezt használd a View-ban: <select asp-for="BreedCode" asp-items="ViewBag.Breeds"></select>
            return View();
        }

        // --- 2. AZ ANYA ELLENŐRZÉSE (AJAX híváshoz) ---
        [HttpGet]
        public async Task<IActionResult> GetDamInfo(string earTag)
        {
            // Keressük meg az állatot, függetlenül a kis/nagybetűtől a fülszámban
            var dam = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .FirstOrDefaultAsync(c => c.EarTag.Trim().ToUpper() == earTag.Trim().ToUpper());

            if (dam == null) return NotFound();

            // Normalizáljuk a korcsoportot az ellenőrzéshez (kisbetűssé tesszük)
            string currentGroup = dam.AgeGroup?.Trim().ToLower() ?? "";

            // Minden lehetséges írásmódot elfogadunk
            var allowedGroups = new[] { "tehén", "tehen", "vemhes üsző", "vemhes uszo" };

            if (!allowedGroups.Contains(currentGroup))
            {
                // Ha nem engedjük elletni, küldjük vissza a pontos okot
                return StatusCode(403, new { message = $"Hiba: {dam.AgeGroup} korcsoportú állat nem ellethető! Csak Tehén vagy Vemhes üsző." });
            }

            return Json(new
            {
                earTag = dam.EarTag,
                enar = dam.EnarNumber,
                ageGroup = dam.AgeGroup,
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
        public async Task<IActionResult> CreateCalf(Cattle calf, string? EarTag2, string? EnarNumber2, string Gender2, double? BirthWeight2, string? IsTwin, string? IsAlive, string? IsAlive2)
        {
            // Checkboxok manuális feldolgozása a hiba elkerülésére
            bool twin = (IsTwin == "on" || IsTwin == "true");
            bool alive1 = (IsAlive == "on" || IsAlive == "true");
            bool alive2 = (IsAlive2 == "on" || IsAlive2 == "true");

            // Az anya adatainak lekérése a Cég és Tenyészet miatt
            var dam = await _context.Cattles.FirstOrDefaultAsync(c => c.EnarNumber == calf.MotherEnar);

            // Mivel a halva születettnek nincs fülszáma, a validátornak megengedjük az üres mezőt
            ModelState.Remove("EarTag");
            ModelState.Remove("EnarNumber");
            ModelState.Remove("PassportNumber");
            ModelState.Remove("AgeGroup");
            ModelState.Remove("IsAlive");
            ModelState.Remove("IsAlive2");
            ModelState.Remove("CurrentHerd");
            ModelState.Remove("Company");

            if (ModelState.IsValid)
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. ELSŐ BORJÚ
                        if (!alive1)
                        {
                            calf.EarTag = "HALVA-" + DateTime.Now.Ticks.ToString().Substring(10); // Belső technikai azonosító
                            calf.EnarNumber = "HALVA-SZÜLETETT";
                        }

                        ProcessNewborn(calf, dam, alive1);
                        _context.Cattles.Add(calf);
                        await _context.SaveChangesAsync();

                        if (!alive1) AddDeathLog(calf, "Halva született");

                        // 2. MÁSODIK BORJÚ (IKER)
                        if (twin)
                        {
                            var secondCalf = new Cattle
                            {
                                BirthDate = calf.BirthDate,
                                MotherEnar = calf.MotherEnar,
                                CurrentHerdId = calf.CurrentHerdId,
                                Gender = (Gender2 == "Bika" ? Gender.Bika : Gender.Üsző),
                                BirthWeight = BirthWeight2 ?? 35,
                                BreedCode = calf.BreedCode,
                                IsTwin = true,
                                IsAlive = alive2
                            };

                            if (!alive2)
                            {
                                secondCalf.EarTag = "HALVA-" + DateTime.Now.Ticks.ToString().Substring(10) + "-2";
                                secondCalf.EnarNumber = "HALVA-SZÜLETETT";
                            }
                            else
                            {
                                secondCalf.EarTag = EarTag2;
                                secondCalf.EnarNumber = EnarNumber2;
                            }

                            ProcessNewborn(secondCalf, dam, alive2);
                            _context.Cattles.Add(secondCalf);
                            await _context.SaveChangesAsync();

                            if (!alive2) AddDeathLog(secondCalf, "Halva született");
                        }

                        // 3. ANYA FRISSÍTÉSE (Csak ha tényleg volt anya a DB-ben)
                        if (dam != null)
                        {
                            dam.DamAgeAtCalving = "Tehén";
                            _context.Update(dam);

                            // Itt a javított sor:
                            var breeding = await _context.BreedingDatas
                                .FirstOrDefaultAsync(b => b.CattleId == dam.Id && b.IsPregnant == true);

                            if (breeding != null)
                            {
                                breeding.IsPregnant = false;
                                _context.Update(breeding);
                            }
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        // SIKERES MENTÉS UTÁN:
                        TempData["SuccessMessage"] = $"Az ellés ({calf.MotherEnar} anyától) sikeresen rögzítve!";

                        // Visszaküldjük az Ellés oldalra az Index helyett
                        return RedirectToAction("Calving");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        ModelState.AddModelError("", "Hiba történt a mentés során: " + ex.Message);
                    }
                }
            }

            // Ha hiba van, vagy az adatok nem validak, újra betöltjük a nézetet
            ViewBag.Herds = new SelectList(_context.Herds.Where(h => !string.IsNullOrEmpty(h.DefaultPrefix)), "Id", "Name");
            return View("Calving", calf);
        }

        // Segédmetódus az alapértékek beállításához
        private void ProcessNewborn(Cattle c, Cattle dam, bool isAlive)
        {
            c.CompanyId = dam?.CompanyId ?? 0;
            c.PassportNumber = "Nincs";
            c.IsActive = isAlive;

            if (isAlive)
            {
                c.AgeGroup = "Itatásos borjú";
            }
            else
            {
                c.AgeGroup = "Halva született"; // <--- Így nem keveredik az élő borjakkal
                c.ExitDate = c.BirthDate;
                c.ExitType = ExitType.Elhullás;
            }
        }

        // Segédmetódus a naplózáshoz
        private void AddDeathLog(Cattle c, string reason)
        {
            var log = new DeathLog
            {
                CattleId = c.Id,
                DeathDate = c.BirthDate,
                Reason = reason,
                EstimatedWeight = c.BirthWeight,
                EarTagAtDeath = c.EarTag,
                EnarNumberAtDeath = c.EnarNumber,
                IsEnarReported = false
            };
            _context.DeathLogs.Add(log);
        }

        // --- ENAR GENERÁLÓ LOGIKA  ---
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


        // --- ENAR BEJELENTŐ LISTA ---
        public async Task<IActionResult> EnarReporting()
        {
            var pendingCalves = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .Where(c => c.AgeGroup == "Itatásos borjú"
                         && c.PassportNumber == "Nincs"
                         && c.IsAlive == true      // <--- Csak az élők
                         && c.IsActive == true)    // <--- Csak az aktívak
                .OrderBy(c => c.BirthDate)
                .ToListAsync();

            return View(pendingCalves);
        }

        // --- XML GENERÁLÁS ÉS STÁTUSZ FRISSÍTÉS ---
        [HttpPost]
        public async Task<IActionResult> GenerateEnarXml(int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0) return BadRequest("Nincs kiválasztott állat.");

            var calves = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .Where(c => selectedIds.Contains(c.Id))
                .ToListAsync();

            // 1. A fő névtér definiálása
            XNamespace ns2 = "http://e5046.client.enar.si.hu";

            // A gyökér elem megkapja az ns2 prefixet
            var root = new XElement(ns2 + "SzmarhaBejelentok",
                new XAttribute(XNamespace.Xmlns + "ns2", ns2.NamespaceName)
            );

            int sorszam = 1;
            foreach (var calf in calves)
            {
                string enarOnly = calf.EnarNumber.Replace("HU", "").Trim();
                string motherEnarOnly = (calf.MotherEnar ?? "").Replace("HU", "").Trim();

                // A belső elemeket NÉVTÉR NÉLKÜL (XName.Get) hozzuk létre, 
                // így nem lesz előttük ns2: és nem lesz bennük xmlns sem.
                var bejelento = new XElement("SzmarhaBejelento",
                    new XElement("Sorszam", sorszam++),
                    new XElement("Tevekenyseg", 3),
                    new XElement("Azonosito", enarOnly),
                    new XElement("SzuletesiDatum", calf.BirthDate.ToString("yyyy-MM-dd")),
                    new XElement("Neme", calf.Gender == Gender.Bika ? 1 : 2),
                    new XElement("SzarvasmarhaFajta", calf.BreedCode),
                    new XElement("Szine", 1),
                    new XElement("TenyeszetKodja", calf.CurrentHerd?.HerdCode ?? "467355"),
                    new XElement("AnyaAzonOrszagkodja", "HU"),
                    new XElement("AnyaAzon", motherEnarOnly),
                    new XElement("Iker-e", calf.IsTwin ? 1 : 2),
                    new XElement("EllesModja", 2),
                    new XElement("BorjuTomege", (int)calf.BirthWeight),
                    new XElement("Surgos-e", "n")
                );
                root.Add(bejelento);

                calf.PassportNumber = "Kérve";
            }

            await _context.SaveChangesAsync();

            // 2. Beállítások: kényszerített UTF-8 és formázás mentesség
            var settings = new XmlWriterSettings
            {
                Indent = false,
                Encoding = new UTF8Encoding(false), // BOM nélküli UTF-8
                OmitXmlDeclaration = false
            };

            string xmlString;
            using (var ms = new MemoryStream())
            {
                settings = new XmlWriterSettings
                {
                    Indent = false,
                    Encoding = new UTF8Encoding(false),
                    OmitXmlDeclaration = false
                };

                using (var writer = XmlWriter.Create(ms, settings))
                {
                    root.WriteTo(writer);
                }
                xmlString = Encoding.UTF8.GetString(ms.ToArray());
            }

            // 1. Idézőjelek javítása: " -> '
            xmlString = xmlString.Replace("\"", "'");

            // 2. Kódolás név javítása: utf-8 -> UTF-8
            // Ezzel a fejlécben pontosan az lesz: encoding='UTF-8'
            xmlString = xmlString.Replace("encoding='utf-8'", "encoding='UTF-8'");

            byte[] fileBytes = Encoding.UTF8.GetBytes(xmlString);
            string fileName = $"bejelent_467355_{DateTime.Now:yyyyMMddHHmmss}.xml";

            return File(fileBytes, "text/xml", fileName);
        }
        [HttpPost]
        public async Task<IActionResult> GenerateEnarPdf(int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0) return BadRequest("Nincs kiválasztott állat.");

            var calves = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .Where(c => selectedIds.Contains(c.Id))
                .OrderBy(c => c.BirthDate)
                .ToListAsync();

            QuestPDF.Settings.License = LicenseType.Community;

            var pdfDoc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                    // FEJLÉC
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("BORJÚ ELLÉS BEJELENTŐ").FontSize(18).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"Tenyészetkód: {calves.FirstOrDefault()?.CurrentHerd?.HerdCode ?? "467355"}").FontSize(11);
                            col.Item().Text("Bejelentő kód: 5046").FontSize(11);
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text($"Készítés ideje: {DateTime.Now:yyyy.MM.dd HH:mm:ss}").FontSize(9).Italic();
                        });
                    });

                    // TARTALOM (TÁBLÁZAT)
                    page.Content().PaddingVertical(15).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);  // Sorszám
                            columns.RelativeColumn(2);   // Fülszám
                            columns.RelativeColumn(3);   // ENAR
                            columns.RelativeColumn(2);   // Születés
                            columns.RelativeColumn(1.5f);// Nem
                            columns.RelativeColumn(3);   // Anya ENAR
                            columns.ConstantColumn(40);  // Iker
                            columns.ConstantColumn(40);  // Súly
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(PdfHeaderStyle).Text("Sorsz.");
                            header.Cell().Element(PdfHeaderStyle).Text("Fülazonosító");
                            header.Cell().Element(PdfHeaderStyle).Text("Borjú ENAR");
                            header.Cell().Element(PdfHeaderStyle).Text("Születés");
                            header.Cell().Element(PdfHeaderStyle).Text("Nem");
                            header.Cell().Element(PdfHeaderStyle).Text("Anya ENAR");
                            header.Cell().Element(PdfHeaderStyle).Text("Iker");
                            header.Cell().Element(PdfHeaderStyle).Text("Súly");
                        });

                        int sorszam = 1;
                        foreach (var calf in calves)
                        {
                            table.Cell().Element(PdfRowStyle).Text(sorszam++.ToString());
                            table.Cell().Element(PdfRowStyle).Text(calf.EarTag ?? "-");
                            table.Cell().Element(PdfRowStyle).Text(calf.EnarNumber ?? "-");
                            table.Cell().Element(PdfRowStyle).Text(calf.BirthDate.ToString("yyyy.MM.dd."));
                            table.Cell().Element(PdfRowStyle).Text(calf.Gender == Gender.Bika ? "Bika" : "Üsző");
                            table.Cell().Element(PdfRowStyle).Text(calf.MotherEnar ?? "-");
                            table.Cell().Element(PdfRowStyle).Text(calf.IsTwin ? "Igen" : "Nem");
                            table.Cell().Element(PdfRowStyle).Text($"{calf.BirthWeight} kg");
                        }
                    });

                    // LÁBLÉC (ÖSSZESÍTŐ)
                    page.Footer().PaddingTop(10).Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Black);
                        col.Item().PaddingTop(5).AlignRight().Text(x =>
                        {
                            x.Span("Összes bejelentett borjú: ").FontSize(11);
                            x.Span($"{calves.Count} db").FontSize(11).SemiBold();
                            x.Span("  |  Össztömeg: ").FontSize(11);
                            x.Span($"{calves.Sum(c => c.BirthWeight)} kg").FontSize(11).SemiBold();
                        });
                    });
                });
            });

            byte[] pdfBytes = pdfDoc.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"bejelent_lista_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // SEGÉDMETÓDUSOK A STÍLUSOKHOZ (A GenerateEnarPdf alá, de a class-on belülre!)
        private IContainer PdfHeaderStyle(IContainer container)
        {
            return container.DefaultTextStyle(x => x.SemiBold())
                            .PaddingVertical(5)
                            .BorderBottom(1)
                            .BorderColor(Colors.Black);
        }

        private IContainer PdfRowStyle(IContainer container)
        {
            return container.PaddingVertical(5)
                            .BorderBottom(1)
                            .BorderColor(Colors.Grey.Lighten3);
        }
        // --- NEMVÁLTÁS LISTA (Csak Itatásos borjú) ---
        public async Task<IActionResult> GenderChangeList()
        {
            var calves = await _context.Cattles
                .Where(c => c.AgeGroup == "Itatásos borjú"
                         && c.IsAlive == true      // <--- Csak az élők
                         && c.IsActive == true)    // <--- Csak az aktívak
                .OrderByDescending(c => c.BirthDate)
                .ToListAsync();

            return View(calves);
        }

        // --- NEMVÁLTÁS VÉGREHAJTÁSA ---
        [HttpPost]
        public async Task<IActionResult> ProcessGenderChange(int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0) return BadRequest();

            var calves = await _context.Cattles.Where(c => selectedIds.Contains(c.Id)).ToListAsync();
            int modifiedCount = 0;

            foreach (var calf in calves)
            {
                // 1. NEM MEGFORDÍTÁSA
                calf.Gender = (calf.Gender == Gender.Bika) ? Gender.Üsző : Gender.Bika;

                // 2. MARHALEVÉL LOGIKA
                if (calf.PassportNumber != "Nincs")
                {
                    // Ha már be volt jelentve (Kérve, Megkérve vagy konkrét szám)
                    // A sorozatszámot (PassportSequence) léptetni kell (pl. 1-ről 2-re)
                    // Ha 0 vagy kisebb (alapértelmezett), akkor 2 lesz (1+1), egyébként pedig növeli az eddigit
                    if (calf.PassportSequence <= 0)
                    {
                        calf.PassportSequence = 2;
                    }
                    else
                    {
                        calf.PassportSequence++;
                    }

                    // A marhalevél számot visszaállítjuk "Kérve" státuszba
                    calf.PassportNumber = "Kérve";
                }

                modifiedCount++;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{modifiedCount} állat neme sikeresen módosítva az adatbázisban. Ne felejtse el egyénileg bejelenteni az ENAR felületén!";

            return RedirectToAction(nameof(GenderChangeList));
        }
        // GET: Cattle/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var cattle = await _context.Cattles.FindAsync(id);
            if (cattle == null) return NotFound();

            // Itt a Name helyett a HerdCode-ot adjuk meg megjelenítendő mezőnek!
            ViewBag.CompanyId = new SelectList(_context.Companies, "Id", "Name", cattle.CompanyId);
            ViewBag.CurrentHerdId = new SelectList(_context.Herds, "Id", "HerdCode", cattle.CurrentHerdId);

            return View(cattle);
        }

        // POST: Cattle/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Cattle cattle)
        {
            // 1. Megkeressük az eredeti rekordot az adatbázisban
            var cattleInDb = await _context.Cattles.FindAsync(id);
            if (cattleInDb == null) return NotFound();

            // 2. Kézzel eltávolítjuk a hibaforrásokat a validációból
            ModelState.Remove("CurrentHerd");
            ModelState.Remove("Company");

            // Ha még így is "Tenyészetkód" hibát kapsz, töröljük konkrétan azt a kulcsot, ami a hibát okozza
            if (ModelState.ContainsKey("CurrentHerd")) ModelState.ClearValidationState("CurrentHerd");

            if (ModelState.IsValid)
            {
                try
                {
                    // 3. Frissítjük az adatokat az adatbázisból betöltött objektumon
                    // Így a navigációs tulajdonságok (CurrentHerd, Company) érintetlenek maradnak
                    _context.Entry(cattleInDb).CurrentValues.SetValues(cattle);

                    await _context.SaveChangesAsync();
                    return RedirectToAction("Index", "Cattles");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Adatbázis hiba: " + ex.Message);
                }
            }

            // Ha hiba van, újraépítjük a listákat a nézethez
            ViewBag.CompanyId = new SelectList(_context.Companies, "Id", "Name", cattle.CompanyId);
            ViewBag.CurrentHerdId = new SelectList(_context.Herds, "Id", "HerdCode", cattle.CurrentHerdId);
            return View(cattle);
        }
        [HttpGet]
        public async Task<IActionResult> RecordDeath(int id)
        {
            var cattle = await _context.Cattles.Include(c => c.CurrentHerd).FirstOrDefaultAsync(x => x.Id == id);
            if (cattle == null) return NotFound();

            // Ha még üres a tábla, feltöltjük az alapértelmezettekkel (csak az első alkalommal)
            if (!_context.DeathReasons.Any())
            {
                var defaultReasons = new[] { "Baleset", "Bélcsavarodás", "Felfúvódás", "Hasmenés", "Tüdőgyulladás" };
                foreach (var r in defaultReasons) _context.DeathReasons.Add(new DeathReason { Name = r });
                await _context.SaveChangesAsync();
            }

            ViewBag.Reasons = await _context.DeathReasons.OrderBy(r => r.Name).ToListAsync();
            return View(cattle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessDeath(int id, DateTime deathDate, string reason, double weight, string receiptNumber)
        {
            var cattle = await _context.Cattles.FindAsync(id);
            if (cattle == null) return NotFound();

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Állat állapotának frissítése
                    cattle.IsActive = false;
                    cattle.IsAlive = false;
                    cattle.ExitDate = deathDate;
                    cattle.ExitType = ExitType.Elhullás;
                    cattle.BirthWeight = (double)weight;

                    // 2. Tranzakció rögzítése a kézi bizonylatszámmal
                    var saleTrans = new SaleTransaction
                    {
                        CattleId = cattle.Id,
                        CustomerId = 2, // ATEV Zrt.
                        SaleDate = deathDate,
                        Type = SaleType.Slaughter,
                        ReceiptNumber = receiptNumber,
                        GrossWeight = (decimal)weight,
                        NetWeight = 0,
                        UnitPrice = 0,
                        TotalNetPrice = 0,
                        DeductionPercentage = 0,
                        IsReported = false
                    };
                    _context.SaleTransactions.Add(saleTrans);

                    // 3. Elhullás naplózása (a havi statisztikához és dögszállítóhoz)
                    var log = new DeathLog
                    {
                        CattleId = cattle.Id,
                        DeathDate = deathDate,
                        Reason = reason,
                        EstimatedWeight = weight,
                        EarTagAtDeath = cattle.EarTag,
                        EnarNumberAtDeath = cattle.EnarNumber
                        // Megjegyzés: Ha a DeathLog táblában is el akarod tárolni a papír tömb számát, 
                        // akkor a DeathLog modellhez is hozzá kell adni egy ReceiptNumber mezőt!
                    };
                    _context.DeathLogs.Add(log);

                    _context.Update(cattle);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = $"Az elhullás rögzítve ({receiptNumber} sz. bizonylattal): {cattle.EarTag}";
                    return RedirectToAction("Index", "Cattles");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Hiba a mentés során: " + ex.Message);
                    return View("RecordDeath", cattle);
                }
            }
        }
        public async Task<IActionResult> DeathLogList(int? year, int? month)
        {
            var y = year ?? DateTime.Now.Year;
            var m = month ?? DateTime.Now.Month;

            var query = _context.DeathLogs
                .Include(d => d.Cattle)
                .Where(d => d.DeathDate.Year == y && d.DeathDate.Month == m);

            var logs = await query.ToListAsync();

            // Statisztika számítása korcsoportonként
            var stats = logs.GroupBy(l => l.Cattle.AgeGroup)
                .Select(g => new {
                    Group = g.Key,
                    Count = g.Count(),
                    TotalWeight = g.Sum(x => x.EstimatedWeight)
                }).ToList();

            ViewBag.Stats = stats;
            ViewBag.Year = y;
            ViewBag.Month = m;

            return View(logs);
        }
        [HttpPost]
        public IActionResult GenerateTransportReceipt(List<int> selectedLogIds, DateTime transportDate)
        {
            var allSelected = _context.DeathLogs
                .Include(d => d.Cattle)
                .Where(d => selectedLogIds.Contains(d.Id))
                .ToList();

            // SEGÉDFÜGGVÉNY: Ha az ENAR nem számokkal kezdődik, vagy benne van a "HALVA" vagy "HULLA" szó, akkor hullaellés
            bool IsStillBorn(DeathLog log) =>
                string.IsNullOrWhiteSpace(log.EnarNumberAtDeath) ||
                log.EnarNumberAtDeath.Contains("HULLA", StringComparison.OrdinalIgnoreCase) ||
                log.EnarNumberAtDeath.Contains("HALVA", StringComparison.OrdinalIgnoreCase);

            // 1. Csoportosítás a mostani szállításból
            var currentNormal = allSelected.Where(l => !IsStillBorn(l)).ToList();
            var currentStillborn = allSelected.Where(l => IsStillBorn(l)).ToList();

            // 2. Múltbóli elmaradások (szigorúan csak rendes ENAR-os állatok!)
            var pendingPassports = _context.DeathLogs
                .Where(d => d.IsTransported && !d.IsPassportSent)
                .AsEnumerable()
                .Where(d => !IsStillBorn(d))
                .ToList();

            // 3. Mentés
            foreach (var log in allSelected)
            {
                log.IsTransported = true;
                log.TransportDate = transportDate;

                if (IsStillBorn(log))
                {
                    log.IsPassportSent = true;
                }
                else
                {
                    bool hasPassport = log.Cattle != null && log.Cattle.PassportNumber != "Nincs" && log.Cattle.PassportNumber != "Kérve";
                    log.IsPassportSent = hasPassport;
                }
            }

            foreach (var p in pendingPassports) { p.IsPassportSent = true; }
            _context.SaveChanges();

            var document = new TransportReceiptDocument(currentNormal, currentStillborn, pendingPassports, transportDate);
            return File(document.GeneratePdf(), "application/pdf", $"Bizonylat_{transportDate:yyyyMMdd}.pdf");
        }
    }   
}
