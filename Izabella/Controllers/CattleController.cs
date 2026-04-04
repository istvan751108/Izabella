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
                    calf.PassportNumber = "Nincs"; // "KÉRVE" helyett
                    calf.AgeGroup = "Itatásos borjú";

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
                            PassportNumber = "Nincs",
                            AgeGroup = "Itatásos borjú"
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


        // --- ENAR BEJELENTŐ LISTA ---
        public async Task<IActionResult> EnarReporting()
        {
            var pendingCalves = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .Where(c => c.AgeGroup == "Itatásos borjú" && c.PassportNumber == "Nincs")
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
                    new XElement("SzarvasmarhaFajta", 22),
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
                .Where(c => c.AgeGroup == "Itatásos borjú")
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
        public async Task<IActionResult> ProcessDeath(int id, DateTime deathDate, string reason, double weight)
        {
            var cattle = await _context.Cattles.FindAsync(id);
            if (cattle == null) return NotFound();

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Állat állapotának frissítése (Ez az Excel "Életút" és "Istálló törlés" megfelelője)
                    cattle.IsActive = false;
                    cattle.IsAlive = false;
                    cattle.ExitDate = deathDate;
                    cattle.ExitType = ExitType.Elhullás; // Idézőjelek nélkül, a típus megadásával;
                    // A súlyt elmenthetjük az utolsó súlyhoz is, ha van ilyen meződ
                    cattle.BirthWeight = weight; // Vagy egy külön ExitWeight mezőbe

                    // 2. Elhullás naplózása (Ez az Excel "Elhullás" lapja)
                    var log = new DeathLog
                    {
                        CattleId = cattle.Id,
                        DeathDate = deathDate,
                        Reason = reason,
                        EstimatedWeight = weight,
                        EarTagAtDeath = cattle.EarTag,
                        EnarNumberAtDeath = cattle.EnarNumber
                    };

                    _context.DeathLogs.Add(log);
                    _context.Update(cattle);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = $"Az elhullás rögzítve: {cattle.EarTag}";
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
    }   
}
