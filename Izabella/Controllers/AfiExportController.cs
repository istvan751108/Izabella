using Izabella.Models;
using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Izabella.Controllers
{
    public class AfiExportController : Controller
    {
        private readonly IzabellaDbContext _context;

        public AfiExportController(IzabellaDbContext context)
        {
            _context = context;
        }

        // 1. Kijelölő felület megjelenítése
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var heifers = await _context.Cattles
                .Where(c => c.IsActive && c.Gender == Gender.Üsző && c.LastInseminationDate == null)
                .OrderBy(c => c.BirthDate)
                .Select(c => new AfiExportViewModel
                {
                    CattleId = c.Id,
                    EarTag = c.EarTag,
                    EnarNumber = c.EnarNumber,
                    BirthDate = c.BirthDate,
                    AgeGroup = c.AgeGroup ?? "Növendék üsző",
                    Stall = c.Stall ?? "",
                    IsSelected = false
                })
                .ToListAsync();

            return View(heifers);
        }

        // 2. A kiválasztott állatokból az afi_ms.dat generálása és letöltése
        [HttpPost]
        public async Task<IActionResult> ExportToAfi(List<AfiExportViewModel> model)
        {
            var selectedIds = model.Where(m => m.IsSelected).Select(m => m.CattleId).ToList();

            if (!selectedIds.Any())
            {
                TempData["Error"] = "Kérjük, jelöljön ki legalább egy üszőt az exporthoz!";
                return RedirectToAction(nameof(Index));
            }

            // Lekérjük a teljes entitásokat az adatbázisból a generáláshoz
            var cattlesToExport = await _context.Cattles
                .Where(c => selectedIds.Contains(c.Id))
                .ToListAsync();

            var sb = new StringBuilder();

            // --- 19 SOROS FIX FEJLÉC BEJEGYZÉSE ---
            sb.AppendLine("CNAfi_Ms.dat, so from management system to Afifarm");
            sb.AppendLine("CN------------------------------------------------");
            sb.AppendLine("CHHeader data");
            sb.AppendLine("DH99000100900000080009000030800090000406000900006240009000072400090000808000900009080");
            sb.AppendLine("VH990001DD      19990625124300Management System       AfiFarm System          3.1     1.1     ");
            sb.AppendLine("CN------------------------------------------------");
            sb.AppendLine("CN*****User dictionary*****");
            sb.AppendLine("CN        |Field||   |List ||   |Item ||   |Descr||");
            sb.AppendLine("DN03186600031861060000318640600003186206000031863400");
            sb.AppendLine("VN031866030540030372100001Priegola Baranda Complete");
            sb.AppendLine("VN031866030540030372100002Priegola Karn Bert");
            sb.AppendLine("VN031866030540030372100003Chirigota Crusader Baranda");
            sb.AppendLine("VN031866030540030372100004Priegola Karn Bravo");
            sb.AppendLine("VN0318660303450304702000022");
            sb.AppendLine("VN0318660303450304702000033");
            sb.AppendLine("VN0318660303450304702000044");
            sb.AppendLine("VN0318660303450304702000055");
            sb.AppendLine("VN0318660303450304702000066");
            sb.AppendLine("VN0318660303450304702000077");

            // --- REKORDOZOTT ADATOK (20. SORTÓL) ---
            foreach (var cow in cattlesToExport)
            {
                // Páros sor: Fix definíciós maszk sor
                sb.AppendLine("DN030532000302590600003032908000031274060000302600200003027706000030417020000304190200003054006000030556060000305521500003060006000030542150000305441500003054606000030548060");

                // Páratlan sor: Az állat adatait tartalmazó, fix szélességű sor összeállítása
                string dataLine = BuildAfiDataLine(cow);
                sb.AppendLine(dataLine);
            }

            // --- LÁBLÉC ---
            sb.AppendLine("EN");
            sb.Append("ZN"); // Az utolsó sor után ne legyen felesleges üres soremelés

            // Fájl byte tömbbé alakítása Windows-1250 kódolással (az Afi a rejtett karakterek miatt ezt szereti)
            byte[] fileBytes = Encoding.GetEncoding("windows-1250").GetBytes(sb.ToString());
            string fileName = $"afi_ms_{DateTime.Now:yyyyMMdd_HHmmss}.dat";

            return File(fileBytes, "application/octet-stream", fileName);
        }

        // Segédmetódus a fix szélességű adatsor precíz legyártásához
        // Segédmetódus a fix szélességű adatsor precíz legyártásához
        private string BuildAfiDataLine(Cattle c)
        {
            // 1. Fix kezdő rész (10 karakter)
            string part1 = "VN030532";

            // 2. Fülszám (6 karakter, balról szóközökkel)
            string part2 = c.EarTag.PadLeft(6);

            // 3. Születési dátum (8 karakter: yyyyMMdd)
            string part3 = c.BirthDate.ToString("yyyyMMdd");

            // 4. Fix rész (6 karakter)
            string part4 = "030052";

            // 5. Istállókód leképezése az egyedi szabályok szerint (2 karakter)
            string stallCode = GetAfiStallCode(c.Stall);
            string part5 = stallCode.PadRight(2);

            // 6. Fix rész (16 karakter)
            string part6 = "0300340000??????";

            // 7. Anya fülszáma az ENAR-ból az új szabály szerint (6 karakter, balról szóközökkel)
            // (A CDV ellenőrző szám előtti 4 számjegy kell, pl. HU3598429554 -> 2955)
            string motherEarTag = GetMotherEarTagFromEnar(c.MotherEnar);
            string part7 = motherEarTag.PadLeft(6);

            // 8. Apa KLSZ (15 karakter, balról szóközökkel)
            string fatherKlsz = string.IsNullOrEmpty(c.FatherKlsz) ? "" : c.FatherKlsz;
            string part8 = fatherKlsz.PadLeft(15);

            // 9. ENAR szám HU nélkül (21 karakter, balról szóközökkel)
            string enarDigits = System.Text.RegularExpressions.Regex.Replace(c.EnarNumber, "[^0-9]", "");
            string part9 = enarDigits.PadLeft(21);

            // 10. Fix záró rész (27 karakter)
            string part10 = "???????????????030044030050";

            // Összefűzzük a darabokat hajszálpontosan (már a hibás pontosvessző nélkül)
            return part1 + part2 + part3 + part4 + part5 + part6 + part7 + part8 + part9 + part10;
        }

        // 1. ÚJ SEGÉDMETÓDUS: Istállókódok egyedi transzformációja az AfiFarm számára
        private string GetAfiStallCode(string? stallName)
        {
            if (string.IsNullOrEmpty(stallName)) return "  ";

            string name = stallName.Trim();

            // Ha pontosan két karakter (pl. "17", "18"), akkor változatlanul hagyjuk
            if (name.Length == 2)
            {
                return name.ToLower();
            }

            // Szöveges megnevezések egyedi leképezése
            if (name.Equals("Borjúkert", StringComparison.OrdinalIgnoreCase)) return "ke";
            if (name.Equals("Pala", StringComparison.OrdinalIgnoreCase)) return "pa";
            if (name.Equals("Borjúnevelő", StringComparison.OrdinalIgnoreCase)) return "bn";

            // Háromjegyű numerikus istállók kezelése (pl. "112" -> "12", "163" -> "63")
            // Megnézzük, hogy 3 jegyű szám-e
            if (name.Length == 3 && int.TryParse(name, out _))
            {
                return name.Substring(1, 2); // Levágja az első karaktert, és visszaadja az utolsó kettőt
            }

            // Biztonsági mentés: ha valami más jönne szembe, az első 2 karakter kisbetűvel
            string fallback = name.Length >= 2 ? name.Substring(0, 2) : name;
            return fallback.ToLower();
        }

        // 2. ÚJ SEGÉDMETÓDUS: Anya fülszámának kinyerése (CDV előtti 4 karakter)
        private string GetMotherEarTagFromEnar(string? motherEnar)
        {
            if (string.IsNullOrEmpty(motherEnar)) return "";

            // Csak a számokat tartjuk meg (pl. HU3598429554 -> 3598429554)
            string cleanDigits = System.Text.RegularExpressions.Regex.Replace(motherEnar, "[^0-9]", "");

            // Egy szabványos magyar ENAR szám 10 számjegyből áll.
            // Ha megvan a szükséges hosszúság, levágjuk a legutolsó karaktert (CDV), és az azt megelőző 4-et vesszük ki.
            if (cleanDigits.Length >= 5)
            {
                // Kivágjuk az utolsó előtti 4 karaktert (Hátulról a 2. 3. 4. 5. karakter kell)
                return cleanDigits.Substring(cleanDigits.Length - 5, 4);
            }

            // Ha az ENAR szám valamiért rövidebb vagy nem szabványos, a biztonság kedvéért a tisztított végződést adjuk vissza
            return cleanDigits.Length > 4 ? cleanDigits.Substring(cleanDigits.Length - 4) : cleanDigits;
        }
    }
}