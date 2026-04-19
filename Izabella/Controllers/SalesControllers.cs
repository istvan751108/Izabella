using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Izabella.Controllers
{
    public partial class SalesController : Controller
    {
        private readonly IzabellaDbContext _context;

        public SalesController(IzabellaDbContext context)
        {
            _context = context;
        }

        // GET: Sales/Index - Az értékesítési napló
        public async Task<IActionResult> Index()
        {
            var sales = await _context.SaleTransactions
                .Include(s => s.Customer)
                .Include(s => s.Cattle)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
            return View(sales);
        }

        // POST: Sales/Checkout - A kijelölt állatok fogadása az Indexről
        [HttpPost]
        public async Task<IActionResult> Checkout(int[] selectedCattleIds)
        {
            if (selectedCattleIds == null || selectedCattleIds.Length == 0)
            {
                TempData["Error"] = "Nincs kijelölve állat az értékesítéshez!";
                return RedirectToAction("Index", "Cattles");
            }

            var selectedCattle = await _context.Cattles
                .Where(c => selectedCattleIds.Contains(c.Id))
                .ToListAsync();

            ViewBag.CustomerId = new SelectList(_context.Customers, "Id", "Name");

            return View(selectedCattle);
        }

        // POST: Sales/ConfirmSale - A véglegesítés és mentés (Már a súlyokkal és bizonylatszámmal)
        [HttpPost]
        public async Task<IActionResult> ConfirmSale(int[] cattleIds, int customerId, SaleType saleType,
    DateTime saleDate, string receiptNumber, decimal[] grossWeights, decimal[] unitPrices, string[] deductions) // string[]-ként fogadjuk a biztonság kedvéért
        {
            if (cattleIds == null) return RedirectToAction("Index", "Cattles");

            for (int i = 0; i < cattleIds.Length; i++)
            {
                var cattle = await _context.Cattles.FindAsync(cattleIds[i]);
                if (cattle == null) continue;

                // --- ÚJ LOGIKA A LEVONÁS MEGHATÁROZÁSÁHOZ ---
                double finalDeduction = 8.0; // Alapértelmezett, ha minden kötél szakad

                // 1. Megpróbáljuk kiolvasni a tömbből, amit a felhasználó beírt
                if (deductions != null && deductions.Length > i)
                {
                    // Megpróbáljuk értelmezni a számot (ponttal és vesszővel is)
                    string rawValue = deductions[i].Replace(",", ".");
                    double.TryParse(rawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out finalDeduction);
                }

                // 2. Ha a kapott érték 0 (vagy hibás volt a beküldés), 
                // akkor az ADATBÁZISBÓL nézzük meg a korcsoportot és kényszerítsük a szabályt
                if (finalDeduction == 0)
                {
                    string group = cattle.AgeGroup ?? "";
                    if (group.Contains("Borjú", StringComparison.OrdinalIgnoreCase)) finalDeduction = 6.0;
                    else if (group.Contains("Tehén", StringComparison.OrdinalIgnoreCase)) finalDeduction = 8.5;
                    else finalDeduction = 8.0;
                }

                // --- SZÁMÍTÁS ---
                decimal gross = grossWeights[i];
                decimal price = unitPrices[i];
                decimal net = gross * (1m - ((decimal)finalDeduction / 100m));

                net = Math.Round(net, 2);
                decimal total = Math.Round(net * price, 2);

                var transaction = new SaleTransaction
                {
                    CattleId = cattle.Id,
                    CustomerId = customerId,
                    SaleDate = saleDate,
                    Type = saleType,
                    ReceiptNumber = receiptNumber,
                    GrossWeight = gross,
                    DeductionPercentage = finalDeduction, // Ezt mentjük el
                    NetWeight = net,
                    UnitPrice = price,
                    TotalNetPrice = total,
                    IsReported = false
                };

                // Állat kivezetése
                cattle.IsActive = false;
                cattle.ExitDate = saleDate;
                cattle.ExitType = (ExitType)saleType;
                if (saleType == SaleType.Slaughter) cattle.IsAlive = false;

                _context.SaleTransactions.Add(transaction);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
        public async Task<IActionResult> Edit(int id)
        {
            var transaction = await _context.SaleTransactions
                .Include(s => s.Cattle)
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

            ViewBag.CustomerId = new SelectList(_context.Customers, "Id", "Name", transaction.CustomerId);
            return View(transaction);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(SaleTransaction model)
        {
            // Itt újra le kell futtatni a nettó súly és végösszeg számítást a módosított bruttó/ár alapján
            model.NetWeight = model.GrossWeight * (decimal)(1 - (model.DeductionPercentage / 100));
            model.TotalNetPrice = model.NetWeight * model.UnitPrice;

            _context.Update(model);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
        [HttpPost]
        public async Task<IActionResult> GenerateEnar5136Package(int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0) return BadRequest("Nincs kijelölt tétel.");

            // Adatok lekérése tenyészettel és marhával
            var transactions = await _context.SaleTransactions
                .Include(s => s.Cattle).ThenInclude(c => c.CurrentHerd)
                .Include(s => s.Customer)
                .Where(t => selectedIds.Contains(t.Id))
                .ToListAsync();

            // Csoportosítás tenyészetkód szerint
            var groupedByHerd = transactions.GroupBy(t => t.Cattle?.CurrentHerd?.HerdCode ?? "467355");

            QuestPDF.Settings.License = LicenseType.Community;

            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var herdGroup in groupedByHerd)
                    {
                        string herdCode = herdGroup.Key;
                        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

                        // --- 1. XML GENERÁLÁS (Tenyészetenként egy fájl) ---
                        XNamespace ns2 = "http://e5136.client.enar.si.hu";
                        var root = new XElement(ns2 + "SzmarhaBejelentok",
                            new XAttribute(XNamespace.Xmlns + "ns2", ns2.NamespaceName)
                        );

                        int sorszam = 1;
                        foreach (var item in herdGroup)
                        {
                            string enarOnly = item.Cattle?.EnarNumber?.Replace("HU", "").Trim() ?? "";
                            string kikerulesKod = item.UnitPrice == 0 ? "3" : (item.Type == SaleType.Export ? "4" : "1");

                            root.Add(new XElement("SzmarhaBejelento",
                                new XElement("Sorszam", sorszam++),
                                new XElement("AzonositoOrszagkodja", "HU"),
                                new XElement("Azonosito", enarOnly),
                                new XElement("KiadasiSorszam", item.Cattle?.PassportSequence.ToString("D2") ?? "01"),
                                new XElement("TenyeszetKod", herdCode),
                                new XElement("KikerulesKodja", kikerulesKod),
                                new XElement("KikerulesDatuma", item.SaleDate.ToString("yyyy-MM-dd"))
                            ));
                            item.IsReported = true;
                        }

                        // XML mentése stringbe az elvárt formázással (idézőjelek és UTF-8 javítás)
                        var settings = new XmlWriterSettings { Indent = false, Encoding = new UTF8Encoding(false) };
                        string xmlString;
                        using (var xmlMs = new MemoryStream())
                        {
                            using (var writer = XmlWriter.Create(xmlMs, settings)) { root.WriteTo(writer); }
                            xmlString = Encoding.UTF8.GetString(xmlMs.ToArray()).Replace("\"", "'").Replace("encoding='utf-8'", "encoding='UTF-8'");
                        }

                        var xmlEntry = archive.CreateEntry($"kiker_{herdCode}_{timestamp}.xml");
                        using (var entryStream = xmlEntry.Open())
                        {
                            byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlString);
                            entryStream.Write(xmlBytes, 0, xmlBytes.Length);
                        }

                        // --- 2. PDF GENERÁLÁS (Tenyészetenként egy dokumentum) ---
                        var pdfDoc = CreateEnar5136Pdf(herdGroup.ToList(), herdCode);
                        var pdfEntry = archive.CreateEntry($"kiker_lista_{herdCode}_{DateTime.Now:yyyyMMdd}.pdf");
                        using (var entryStream = pdfEntry.Open())
                        {
                            byte[] pdfBytes = pdfDoc.GeneratePdf();
                            entryStream.Write(pdfBytes, 0, pdfBytes.Length);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return File(memoryStream.ToArray(), "application/zip", $"ENAR_5136_CSOMAG_{DateTime.Now:yyyyMMdd}.zip");
            }
        }
        public string GenerateEnar5135Xml(List<Cattle> selectedCattle, string newHerdCode, string certNumber, DateTime moveDate, string stampNumber = "0134")
        {
            XNamespace ns2 = "http://e5135.client.enar.si.hu";
            var root = new XElement(ns2 + "SzmarhaBejelentok", new XAttribute(XNamespace.Xmlns + "ns2", ns2.NamespaceName));

            int sorszam = 1;
            foreach (var cattle in selectedCattle)
            {
                string cleanEnar = cattle.EnarNumber.Replace("HU", "").Trim();
                string cleanMotherEnar = cattle.MotherEnar.Replace("HU", "").Trim();
                string currentSequence = cattle.PassportSequence.ToString("D2");

                root.Add(new XElement("SzmarhaBejelento",
                    new XElement("Sorszam", sorszam++),
                    new XElement("AzonositoOrszagkodja", "HU"),
                    new XElement("Azonosito", cleanEnar),
                    new XElement("KiadasiSorszam", currentSequence),
                    new XElement("AnyaAzonOrszagkodja", "348"),
                    new XElement("AnyaAzon", cleanMotherEnar),
                    new XElement("TenyeszetKod", newHerdCode),
                    new XElement("BekerulesDatuma", moveDate.ToString("yyyy-MM-dd")),
                    new XElement("BizonyitvanySorszama", certNumber),
                    new XElement("BizonyitvanyDatuma", moveDate.ToString("yyyy-MM-dd")),
                    new XElement("KamaraiBelyegzoSzama", stampNumber)
                ));
            }

            var settings = new XmlWriterSettings
            {
                Indent = false,
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = false
            };

            using (var ms = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(ms, settings)) { root.Save(writer); }
                // Kényszerítsük a nagybetűs UTF-8-at a stringben
                return Encoding.UTF8.GetString(ms.ToArray()).Replace("utf-8", "UTF-8");
            }
        }

        // PDF Dokumentum struktúra metódus
        private IDocument CreateEnar5136Pdf(List<SaleTransaction> items, string herdCode)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("SZARVASMARHA KIKERÜLÉSI BIZONYLAT (5136)").FontSize(16).SemiBold().FontColor(Colors.Red.Medium);
                            col.Item().Text($"Kijelentő tenyészet: {herdCode}").FontSize(11);
                        });
                        row.RelativeItem().AlignRight().Text($"{DateTime.Now:yyyy.MM.dd}").FontSize(10);
                    });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);  // Sorszám
                            columns.RelativeColumn(3);   // ENAR
                            columns.RelativeColumn(2);   // Dátum
                            columns.RelativeColumn(1.5f);// Kód
                            columns.RelativeColumn(4);   // Partner/Vevő
                            columns.RelativeColumn(2);   // Marhalevél
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(PdfHeaderStyle).Text("Ssz.");
                            header.Cell().Element(PdfHeaderStyle).Text("Állat ENAR");
                            header.Cell().Element(PdfHeaderStyle).Text("Dátum");
                            header.Cell().Element(PdfHeaderStyle).Text("Kód");
                            header.Cell().Element(PdfHeaderStyle).Text("Partner / Célállomás");
                            header.Cell().Element(PdfHeaderStyle).Text("Melléklet");
                        });

                        int sorszam = 1;
                        foreach (var item in items)
                        {
                            string kikerulesTipus = item.UnitPrice == 0 ? "3 (Elh.)" : (item.Type == SaleType.Export ? "4 (Exp.)" : "1 (Ért.)");

                            table.Cell().Element(PdfRowStyle).Text(sorszam++.ToString());
                            table.Cell().Element(PdfRowStyle).Text(item.Cattle?.EnarNumber ?? "-");
                            table.Cell().Element(PdfRowStyle).Text(item.SaleDate.ToString("yyyy.MM.dd"));
                            table.Cell().Element(PdfRowStyle).Text(kikerulesTipus);
                            table.Cell().Element(PdfRowStyle).Text($"{item.Customer?.Name}");
                            table.Cell().Element(PdfRowStyle).Text(item.Cattle?.PassportNumber ?? "01");
                        }
                    });

                    page.Footer().PaddingTop(20).Column(col => {
                        col.Item().Row(row => {
                            row.RelativeItem().PaddingTop(20).Column(c => {
                                c.Item().LineHorizontal(0.5f);
                                c.Item().AlignCenter().Text("Állattartó aláírása");
                            });
                            row.ConstantItem(50);
                            row.RelativeItem().PaddingTop(20).Column(c => {
                                c.Item().LineHorizontal(0.5f);
                                c.Item().AlignCenter().Text("Hatósági állatorvos / Szállító");
                            });
                        });
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

        public async Task<IActionResult> MonthlyReport(int? year, int? month)
        {
            var y = year ?? DateTime.Now.Year;
            var m = month ?? DateTime.Now.Month;

            // 1. Értékesítések és Elhullások (mint eddig)
            var transactions = await _context.SaleTransactions
                .Include(s => s.Customer)
                .Include(s => s.Cattle).ThenInclude(c => c.CurrentHerd)
                .Where(s => s.SaleDate.Year == y && s.SaleDate.Month == m)
                .ToListAsync();

            // 2. Ellések (az adott hónapban született borjak)
            // Beemeljük az anya adatait is a korcsoport miatt
            var newborns = await _context.Cattles
                .Where(c => c.BirthDate.Year == y && c.BirthDate.Month == m && c.MotherEnar != null)
                .ToListAsync();

            // Anya adatok lekérése a korcsoport statisztikához
            var motherEnars = newborns.Select(n => n.MotherEnar).Distinct().ToList();
            var mothers = await _context.Cattles
                .Where(c => motherEnars.Contains(c.EnarNumber))
                .ToListAsync();

            ViewBag.Year = y;
            ViewBag.Month = m;
            ViewBag.Newborns = newborns;
            ViewBag.Mothers = mothers;
            ViewBag.Herds = await _context.Herds.OrderBy(h => h.Name).ToListAsync();
            return View(transactions);
        }
        // SalesController.cs
        [HttpPost]
        public async Task<IActionResult> UndoReport(int id)
        {
            var transaction = await _context.SaleTransactions
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null) return NotFound();

            // 1. Csak a jelentési státuszt állítjuk vissza
            transaction.IsReported = false;

            // 2. Opcionális: Ha a sorszámot is vissza akarod görgetni a marhánál, 
            // mert az XML generáláskor növelted:
            var cattle = await _context.Cattles.FirstOrDefaultAsync(c => c.Id == transaction.CattleId);
            if (cattle != null && cattle.PassportNumber == "Kérve")
            {
                // Csak akkor nyúlunk hozzá, ha még mindig ebben a "köztes" állapotban van
                if (cattle.PassportSequence > 1) cattle.PassportSequence -= 1;

                // Itt döntened kell: ha visszavonod a jelentést, az állat 
                // technikailag még a régi helyén van az adatbázis szerint? 
                // Ha igen, akkor:
                // cattle.PassportNumber = "Visszavont"; 
            }

            _context.Update(transaction);
            await _context.SaveChangesAsync();

            // Visszatérünk a listához - most már újra ott lesz a checkbox!
            return RedirectToAction(nameof(MonthlyReport));
        }
        [HttpPost]
        public async Task<IActionResult> ProcessOwnerChange(int[] selectedCattleIds, string targetHerdCode, string targetOwnerName, string certNumber, string stampNumber, DateTime moveDate)
        {
            if (selectedCattleIds == null || selectedCattleIds.Length == 0)
                return Content("Hiba: Nincs kijelölt állat!");

            var targetHerd = await _context.Herds.AsNoTracking()
                .FirstOrDefaultAsync(h => h.HerdCode == targetHerdCode);

            if (targetHerd == null)
                return Content($"Hiba: A cél tenyészet ({targetHerdCode}) nem található!");

            var selectedCattle = await _context.Cattles
                .Include(c => c.CurrentHerd)
                .Include(c => c.Company)
                .Where(c => selectedCattleIds.Contains(c.Id))
                .ToListAsync();

            QuestPDF.Settings.License = LicenseType.Community;

            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    // --- A. 5135 XML GENERÁLÁS (Vétel) ---
                    string xml5135 = GenerateEnar5135Xml(selectedCattle, targetHerdCode, certNumber, moveDate, stampNumber);
                    var entry5135 = archive.CreateEntry($"ENAR_5135_vetel_{targetHerdCode}_{DateTime.Now:yyyyMMdd}.xml");
                    using (var writer = new StreamWriter(entry5135.Open(), new UTF8Encoding(false)))
                    {
                        await writer.WriteAsync(xml5135);
                    }

                    // --- B. 5136 XML GENERÁLÁS (Eladás) ---
                    // Itt deklaráljuk a változót, amit a hiba hiányolt
                    var groupedByOldHerd = selectedCattle.GroupBy(c => c.CurrentHerd?.HerdCode ?? "ISMERETLEN");

                    foreach (var herdGroup in groupedByOldHerd)
                    {
                        string oldHerdCode = herdGroup.Key;
                        XNamespace ns2 = "http://e5136.client.enar.si.hu";
                        var root5136 = new XElement(ns2 + "SzmarhaBejelentok", new XAttribute(XNamespace.Xmlns + "ns2", ns2.NamespaceName));

                        int sorszam = 1;
                        foreach (var cattle in herdGroup)
                        {
                            // Itt volt a hiba: külön sorba szedjük az adat-előkészítést
                            string enarRaw = cattle.EnarNumber ?? "";
                            string enarOnly = enarRaw.Replace("HU", "").Trim();
                            string sequence = cattle.PassportSequence.ToString("D2");

                            // Új elem létrehozása és hozzáadása
                            var bejelento = new XElement("SzmarhaBejelento",
                                new XElement("Sorszam", sorszam++),
                                new XElement("AzonositoOrszagkodja", "HU"),
                                new XElement("Azonosito", enarOnly),
                                new XElement("KiadasiSorszam", sequence),
                                new XElement("TenyeszetKod", oldHerdCode),
                                new XElement("KikerulesKodja", "1"),
                                new XElement("KikerulesDatuma", moveDate.ToString("yyyy-MM-dd")),
                                new XElement("TenyeszetKodCel", targetHerdCode)
                            );
                            root5136.Add(bejelento);
                        }

                        var entry5136 = archive.CreateEntry($"ENAR_5136_eladas_{oldHerdCode}_{DateTime.Now:yyyyMMdd}.xml");
                        using (var entryStream = entry5136.Open())
                        {
                            var settings = new XmlWriterSettings { Indent = false, Encoding = new UTF8Encoding(false) };
                            using (var ms = new MemoryStream())
                            {
                                using (var writer = XmlWriter.Create(ms, settings)) { root5136.Save(writer); }
                                string xmlContent = Encoding.UTF8.GetString(ms.ToArray()).Replace("utf-8", "UTF-8");
                                byte[] bytes = Encoding.UTF8.GetBytes(xmlContent);
                                entryStream.Write(bytes, 0, bytes.Length);
                            }
                        }

                        // --- C. PDF GENERÁLÁS ---
                        var fakeTransactions = herdGroup.Select(c => new SaleTransaction
                        {
                            Cattle = c,
                            SaleDate = moveDate,
                            Type = SaleType.OwnershipChange,
                            Customer = new Customer { Name = targetOwnerName }
                        }).ToList();

                        var pdfDoc = CreateEnar5136Pdf(fakeTransactions, oldHerdCode);
                        var pdfEntry = archive.CreateEntry($"Kiserojegy_5136_{oldHerdCode}_to_{targetHerdCode}.pdf");
                        using (var entryStream = pdfEntry.Open())
                        {
                            byte[] pdfBytes = pdfDoc.GeneratePdf();
                            await entryStream.WriteAsync(pdfBytes, 0, pdfBytes.Length);
                        }
                    }

                    // --- D. ADATBÁZIS MENTÉS ---
                    using (var dbTransaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            // A cél cég ID-ja (ha a Herd-ben van CompanyId)
                            var targetCompanyId = targetHerd.CompanyId;

                            foreach (var cattle in selectedCattle)
                            {
                                cattle.PassportNumber = "Kérve";
                                cattle.PassportSequence += 1;
                                cattle.CurrentHerdId = targetHerd.Id;
                                cattle.CompanyId = targetCompanyId;
                                cattle.ExitDate = moveDate;
                                cattle.ExitType = ExitType.Tulajdonosváltás;
                                cattle.IsActive = true;

                                // Navigációs tulajdonságok ürítése a mentéshez
                                cattle.CurrentHerd = null;
                                cattle.Company = null;

                                _context.Attach(cattle);
                                _context.Entry(cattle).State = EntityState.Modified;

                                // Kényszerített frissítés
                                _context.Entry(cattle).Property(x => x.CompanyId).IsModified = true;
                                _context.Entry(cattle).Property(x => x.CurrentHerdId).IsModified = true;
                            }

                            // A tranzakciók frissítése (IsReported = true)
                            // Megkeressük a tranzakciókat, amik ezekhez az állatokhoz tartoznak
                            var transactionsToUpdate = await _context.SaleTransactions
                                .Where(t => selectedCattleIds.Contains(t.CattleId) && t.Type == SaleType.OwnershipChange && !t.IsReported)
                                .ToListAsync();

                            foreach (var trans in transactionsToUpdate)
                            {
                                trans.IsReported = true;
                                // Ha van Note meződ, oda írhatsz, ha nincs, hagyd el ezt a sort:
                                // trans.Note = "Tulajdonosváltás lejelentve"; 
                            }

                            await _context.SaveChangesAsync();
                            await dbTransaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
                            await dbTransaction.RollbackAsync();
                            return Content("Adatbázis hiba: " + ex.Message);
                        }
                    }
                }
                return File(memoryStream.ToArray(), "application/zip", $"TULAJDONOSVALTAS_{DateTime.Now:yyyyMMdd}.zip");
            }
        }
    }
}