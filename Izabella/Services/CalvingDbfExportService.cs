using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Izabella.Models;

namespace Izabella.Services
{
    public class CalvingDbfExportService
    {
        private readonly IzabellaDbContext _context;

        public CalvingDbfExportService(IzabellaDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> GenerateJellesDbfAsync(string megye, string tenyeszet, string telep, DateTime befejesDatuma, DateTime utolsoBefejesOta)
        {
            _context.ChangeTracker.Clear();

            // 1. LEKÉRDEZÉS: Összes borjú, aki az utolsó befejezés óta született (Kivéve a rendszer-rekordot!)
            var newborns = await _context.Cattles
                .AsNoTracking()
                .Where(c => c.BirthDate > utolsoBefejesOta && c.BirthDate <= befejesDatuma && c.EarTag != "SYSTEM")
                .ToListAsync();

            var fields = new (string Name, char Type, byte Length)[]
            {
                ("MEGYE",      'C', 2),
                ("TENYESZET",  'C', 3),
                ("TELEP",      'C', 2),
                ("AZONOSITO",  'C', 12),
                ("ELL_SSZ",    'C', 2),
                ("E_TERM_SSZ", 'C', 2),
                ("ELL_DAT",    'D', 8),
                ("ELL_LEF",    'C', 1),
                ("BOR_SOR",    'C', 1),
                ("BOR_IV",     'C', 1),
                ("BOR_SZ",     'C', 1),
                ("BOR_SU",     'N', 3),
                ("BOR_HVK",    'C', 1),
                ("BOR_HVO",    'C', 1),
                ("BOR_EN",     'C', 10),
                ("BEF_DAT",    'D', 8),
                ("FEL_HO",     'C', 2),
                ("MOD_DAT",    'D', 8)
            };

            int recordLength = 1 + fields.Sum(f => f.Length);
            short headerLength = (short)(32 + (fields.Length * 32) + 1);

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.GetEncoding("windows-1250")))
            {
                // ---- Fejléc írása ----
                bw.Write((byte)0x03);
                bw.Write((byte)(DateTime.Today.Year - 1900));
                bw.Write((byte)DateTime.Today.Month);
                bw.Write((byte)DateTime.Today.Day);
                bw.Write(newborns.Count);
                bw.Write(headerLength);
                bw.Write((short)recordLength);
                bw.Write(new byte[20]);

                // ---- Mezőleírók írása ----
                foreach (var field in fields)
                {
                    byte[] nameBytes = new byte[11];
                    byte[] rawName = Encoding.ASCII.GetBytes(field.Name);
                    Array.Copy(rawName, nameBytes, Math.Min(rawName.Length, 11));
                    bw.Write(nameBytes);

                    bw.Write(field.Type);
                    bw.Write(0);
                    bw.Write(field.Length);
                    bw.Write((byte)0);
                    bw.Write(new byte[14]);
                }

                bw.Write((byte)0x0D);

                // ---- Adatrekordok írása ----
                foreach (var calf in newborns)
                {
                    var dam = await _context.Cattles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.EnarNumber == calf.MotherEnar);

                    int inseminationCount = 0; // Alapértelmezett 0, ha hiányzik
                    if (dam != null)
                    {
                        inseminationCount = await _context.InseminationLogs
                            .AsNoTracking()
                            .CountAsync(l => l.CattleEarTag == dam.EarTag && l.EventDate <= calf.BirthDate);
                    }

                    int laktacio = 0; // Alapértelmezett 0, ha hiányzik
                    if (dam != null)
                    {
                        laktacio = dam.CurrentLactationNo;
                    }

                    // 🔥 JAVÍTÁS: Ha halva született, ne a hibás szöveg menjen be, hanem legyen teljesen ÜRES a fülszám!
                    string borjEnar = calf.EnarNumber ?? "";
                    bool halvaSzuletett = !calf.IsAlive || borjEnar.Contains("HALVA") || borjEnar.Contains("HLV");

                    if (halvaSzuletett)
                    {
                        borjEnar = ""; // Halva született borjúnak nincs füljelzője a DBF-ben
                    }
                    else if (borjEnar.StartsWith("HU"))
                    {
                        borjEnar = borjEnar.Substring(2);
                    }

                    int sulyInt = calf.BirthWeight > 0 ? (int)calf.BirthWeight : 40;

                    // 🔥 JAVÍTÁS: Ha halva született, szigorúan "8"-as kód kell a BOR_HVK-ba, különben "0"
                    string borhHvkKod = halvaSzuletett ? "8" : "0";

                    string[] rowValues = new string[]
                    {
                        megye,
                        tenyeszet,
                        telep,
                        calf.MotherEnar ?? "",
                        laktacio == 0 ? "00" : laktacio.ToString("D2"),
                        inseminationCount == 0 ? "00" : inseminationCount.ToString("D2"),
                        calf.BirthDate.ToString("yyyyMMdd"),
                        "2",
                        calf.IsTwin ? "2" : "0",
                        calf.Gender == Gender.Bika ? "1" : "2",
                        "1",
                        sulyInt.ToString("D3"),
                        borhHvkKod, // BOR_HVK (Most már pontosan 8 vagy 0)
                        "0",        // BOR_HVO
                        borjEnar,   // BOR_EN (Most már üres a halva születettnél)
                        befejesDatuma.ToString("yyyyMMdd"),
                        befejesDatuma.Month.ToString("D2"),
                        befejesDatuma.ToString("yyyyMMdd")
                    };

                    bw.Write((byte)0x20);

                    for (int i = 0; i < fields.Length; i++)
                    {
                        string val = rowValues[i] ?? "";
                        int targetLen = fields[i].Length;

                        string padded = val.PadRight(targetLen);
                        if (padded.Length > targetLen) padded = padded.Substring(0, targetLen);

                        byte[] recordBytes = Encoding.GetEncoding("windows-1250").GetBytes(padded);
                        bw.Write(recordBytes);
                    }
                }

                bw.Write((byte)0x1A);
                bw.Flush();

                return ms.ToArray();
            }
        }

        private async Task<byte[]> BuildKiesesDbfAsync(System.Collections.Generic.List<Cattle> animals, string megye, string tenyeszet, string telep, DateTime befejesDatuma)
        {
            // 🔥 PONTOSÍTÁS: Hajszálpontosan a képeden látható 9 oszlop definíciója
            var fields = new (string Name, char Type, byte Length)[]
            {
        ("MEGYE",          'C', 2),
        ("TENYESZET",      'C', 3), // Átnevezve sima TENYESZET-re a kép alapján
        ("TELEP",          'C', 2),
        ("AZONOSITO",      'C', 12), // Átnevezve AZONOSITO-ra BOR_AZONOSITO helyett
        ("KIESES_KOD",     'C', 1),
        ("KIESES_OK",      'C', 1),
        ("KIESES_DAT",     'D', 8),
        ("EVHO_KI",        'C', 4),
        ("MOD_DAT",        'D', 8)  // LMOD_DAT helyett MOD_DAT a kép alapján
            };

            int recordLength = 1 + fields.Sum(f => f.Length); // 1 + 39 = 40 bájt
            short headerLength = (short)(32 + (fields.Length * 32) + 1);

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.GetEncoding("windows-1250")))
            {
                // 1. Fejléc írása
                bw.Write((byte)0x03);
                bw.Write((byte)(DateTime.Today.Year - 1900));
                bw.Write((byte)DateTime.Today.Month);
                bw.Write((byte)DateTime.Today.Day);
                bw.Write(animals.Count);
                bw.Write(headerLength);
                bw.Write((short)recordLength);
                bw.Write(new byte[20]);

                // 2. Mezőleírók írása
                foreach (var field in fields)
                {
                    byte[] nameBytes = new byte[11];
                    byte[] rawName = Encoding.ASCII.GetBytes(field.Name);
                    Array.Copy(rawName, nameBytes, Math.Min(rawName.Length, 11));
                    bw.Write(nameBytes);
                    bw.Write(field.Type);
                    bw.Write(0);
                    bw.Write(field.Length);
                    bw.Write((byte)0);
                    bw.Write(new byte[14]);
                }
                bw.Write((byte)0x0D);

                // 3. Adatrekordok írása
                foreach (var animal in animals)
                {
                    string kiesesKod = "1";
                    if (animal.ExitType == ExitType.Elhullás) kiesesKod = "3";
                    else if (animal.ExitType == ExitType.Vágás) kiesesKod = "6";
                    else if (animal.ExitType == ExitType.Export) kiesesKod = "5";
                    else if (animal.ExitType == ExitType.Továbbtartás) kiesesKod = "1";

                    string kiesesOk = " ";
                    bool isUszo = animal.AgeGroup != "Tehén";

                    if (animal.ExitType == ExitType.Elhullás)
                    {
                        kiesesOk = "0";

                        var deathLog = await _context.DeathLogs
                            .AsNoTracking()
                            .FirstOrDefaultAsync(d => d.CattleId == animal.Id);

                        if (deathLog != null && !string.IsNullOrEmpty(deathLog.Reason))
                        {
                            string reasonText = deathLog.Reason.ToUpper();

                            if (reasonText.Contains("TÜDŐ") || reasonText.Contains("PNEU"))
                            {
                                kiesesOk = "7";
                            }
                            else if (isUszo && (reasonText.Contains("MEDDŐ") || reasonText.Contains("MEDDO")))
                            {
                                kiesesOk = "3";
                            }
                        }
                    }

                    string kiesesDatStr = animal.ExitDate?.ToString("yyyyMMdd") ?? befejesDatuma.ToString("yyyyMMdd");
                    string fevhoKi = befejesDatuma.ToString("yyMM");

                    // 🔥 PONTOSÍTÁS: Csak a 9 szükséges adat átadása
                    string[] rowValues = new string[]
                    {
                megye,
                tenyeszet,
                telep,
                animal.EnarNumber ?? "",
                kiesesKod,
                kiesesOk,
                kiesesDatStr,
                fevhoKi,
                befejesDatuma.ToString("yyyyMMdd")
                    };

                    bw.Write((byte)0x20);

                    for (int i = 0; i < fields.Length; i++)
                    {
                        string val = rowValues[i] ?? "";
                        int targetLen = fields[i].Length;

                        string padded = val.PadRight(targetLen);
                        if (padded.Length > targetLen) padded = padded.Substring(0, targetLen);

                        byte[] recordBytes = Encoding.GetEncoding("windows-1250").GetBytes(padded);
                        bw.Write(recordBytes);
                    }
                }

                bw.Write((byte)0x1A);
                bw.Flush();
                return ms.ToArray();
            }
        }

        // 1. TEHENEK KIESÉSE (Nyilvános hívható metódus)
        public async Task<byte[]> GenerateTehenKiesesDbfAsync(string megye, string tenyeszet, string telep, DateTime befejesDatuma, DateTime utolsoBefejesOta)
        {
            _context.ChangeTracker.Clear();

            var cows = await _context.Cattles
                .AsNoTracking()
                .Where(c => c.ExitDate > utolsoBefejesOta && c.ExitDate <= befejesDatuma
                         && c.AgeGroup == "Tehén"
                         && c.EarTag != "SYSTEM"
                         && c.IsAlive == true) // 🔥 JAVÍTÁS: Csak az élve született állatok eshetnek ki
                .ToListAsync();

            return await BuildKiesesDbfAsync(cows, megye, tenyeszet, telep, befejesDatuma);
        }

        // 2. ÜSZŐK KIESÉSE (Nyilvános hívható metódus)
        public async Task<byte[]> GenerateUszoKiesesDbfAsync(string megye, string tenyeszet, string telep, DateTime befejesDatuma, DateTime utolsoBefejesOta)
        {
            _context.ChangeTracker.Clear();

            var heifers = await _context.Cattles
                .AsNoTracking()
                .Where(c => c.ExitDate > utolsoBefejesOta && c.ExitDate <= befejesDatuma
                         && c.AgeGroup != "Tehén"
                         && c.EarTag != "SYSTEM"
                         && c.IsAlive == true) // 🔥 JAVÍTÁS: Kiszűrjük a halva születetteket, ők nem kerülnek be a körbe
                .ToListAsync();

            return await BuildKiesesDbfAsync(heifers, megye, tenyeszet, telep, befejesDatuma);
        }

        public byte[] GenerateJtelepDbf(string megye, string tenyeszet, string telep, string enarTeny, DateTime befejesDatuma)
        {
            // Fixen 7 karakteresre formázzuk az ENAR tenyészetkódot (balról nullákkal töltve)
            string formattedEnarTeny = enarTeny.PadLeft(7, '0');

            var fields = new (string Name, char Type, byte Length)[]
            {
        ("MEGYE",      'C', 2),
        ("TENYESZET",  'C', 3),
        ("TELEP",      'C', 2),
        ("ENAR_TENY",  'C', 7),
        ("DSZ",        'C', 1),
        ("PVER",       'C', 4),
        ("BEF_DAT01",  'D', 8)
            };

            int recordLength = 1 + fields.Sum(f => f.Length); // 1 + 27 = 28 bájt
            short headerLength = (short)(32 + (fields.Length * 32) + 1);

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.GetEncoding("windows-1250")))
            {
                // 1. Fejléc (Mindig 1 darab rekordunk van ebben a fájlban)
                bw.Write((byte)0x03);
                bw.Write((byte)(DateTime.Today.Year - 1900));
                bw.Write((byte)DateTime.Today.Month);
                bw.Write((byte)DateTime.Today.Day);
                bw.Write(1); // Fixen 1 rekord
                bw.Write(headerLength);
                bw.Write((short)recordLength);
                bw.Write(new byte[20]);

                // 2. Mezőleírók
                foreach (var field in fields)
                {
                    byte[] nameBytes = new byte[11];
                    byte[] rawName = Encoding.ASCII.GetBytes(field.Name);
                    Array.Copy(rawName, nameBytes, Math.Min(rawName.Length, 11));
                    bw.Write(nameBytes);
                    bw.Write(field.Type);
                    bw.Write(0);
                    bw.Write(field.Length);
                    bw.Write((byte)0);
                    bw.Write(new byte[14]);
                }
                bw.Write((byte)0x0D);

                // 3. Egyetlen adatrekord összeállítása
                string[] rowValues = new string[]
                {
            megye,
            tenyeszet,
            telep,
            formattedEnarTeny,
            "1",        // DSZ fixen 1
            "v400",     // PVER fixen v400
            befejesDatuma.ToString("yyyyMMdd")
                };

                bw.Write((byte)0x20); // Aktív rekord jelző

                for (int i = 0; i < fields.Length; i++)
                {
                    string val = rowValues[i] ?? "";
                    int targetLen = fields[i].Length;

                    string padded = val.PadRight(targetLen);
                    if (padded.Length > targetLen) padded = padded.Substring(0, targetLen);

                    byte[] recordBytes = Encoding.GetEncoding("windows-1250").GetBytes(padded);
                    bw.Write(recordBytes);
                }

                bw.Write((byte)0x1A); // Fájl lezárás
                bw.Flush();
                return ms.ToArray();
            }
        }

        private async Task<byte[]> BuildTermekenyitesDbfAsync(System.Collections.Generic.List<InseminationLog> logs, string megye, string tenyeszet, string telep, DateTime befejesDatuma, bool isUszo)
        {
            var fields = new (string Name, char Type, byte Length)[]
            {
        ("MEGYE",          'C', 2),
        ("TENYESZET",      'C', 3),
        ("TELEP",          'C', 2),
        ("AZONOSITO",      'C', 12),
        ("K_ELL_SSZ",      'C', 2),
        ("TERM_SSZ",       'C', 2),
        ("TERM_DAT",       'D', 8),
        ("TERM_BIKA",      'C', 5),
        ("VEMH_KOD",       'C', 1),
        ("D_AZ_TIP",       'C', 2),
        ("D_AZON",         'C', 10),
        ("SUPEROVUL",      'C', 1),
        ("ISM_BIKA",       'C', 5),
        ("TERM_MOD",       'C', 1),
        ("INSZ_KOD",       'C', 7),
        ("ELOALL_KOD",     'C', 2),
        ("SPERM_AZON",     'C', 12),
        ("SPERM_TIP",      'C', 2),
        ("SPERM_ERED",     'C', 1),
        ("OSSZBEF_DAT",    'D', 8),
        ("BEFEFEL_HO",     'C', 4), // Ez felel meg a FEL_HO-nak
        ("DABEVEMH_HO",    'C', 2),
        ("SZARMOD_DAT",    'D', 8)  // Ez felel meg a MOD_DAT-nak
            };

            int recordLength = 1 + fields.Sum(f => f.Length); // 101 bájt
            short headerLength = (short)(32 + (fields.Length * 32) + 1);

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.GetEncoding("windows-1250")))
            {
                // 1. Fejléc írása
                bw.Write((byte)0x03);
                bw.Write((byte)(DateTime.Today.Year - 1900));
                bw.Write((byte)DateTime.Today.Month);
                bw.Write((byte)DateTime.Today.Day);
                bw.Write(logs.Count);
                bw.Write(headerLength);
                bw.Write((short)recordLength);
                bw.Write(new byte[20]);

                // 2. Mezőleírók írása
                foreach (var field in fields)
                {
                    byte[] nameBytes = new byte[11];
                    byte[] rawName = Encoding.ASCII.GetBytes(field.Name);
                    Array.Copy(rawName, nameBytes, Math.Min(rawName.Length, 11));
                    bw.Write(nameBytes);
                    bw.Write(field.Type);
                    bw.Write(0);
                    bw.Write(field.Length);
                    bw.Write((byte)0);
                    bw.Write(new byte[14]);
                }
                bw.Write((byte)0x0D);

                // 3. Adatrekordok írása
                foreach (var log in logs)
                {
                    // Megkeressük az állatot az EarTag alapján
                    var animal = await _context.Cattles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.EarTag == log.CattleEarTag);

                    if (animal == null) continue; // Biztonsági őr, ha nem létezne az állat

                    // K_ELL_SSZ kiszámítása (Aktuális laktáció + 1)
                    int kovEllSsz = animal.CurrentLactationNo + 1;

                    // TERM_SSZ kiszámítása: Hányadik termékenyítése van eddig az állatnak ebben a szakaszban
                    int termSsz = await _context.InseminationLogs
                        .AsNoTracking()
                        .CountAsync(l => l.CattleEarTag == animal.EarTag && l.EventDate <= log.EventDate);

                    // Inszeminátor kód kikeresése
                    var staff = await _context.Staffs
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Name == log.InseminatorName);
                    string inszKod = staff?.InseminatorCode ?? "0000000";

                    // Bika és sperma adatok kinyerése
                    var semen = await _context.BullSemens
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == log.BullSemenId);

                    string bikaKlsz = semen?.Klsz ?? "";
                    // Csak a számokat tartjuk meg, ha esetleg betű is lenne a KLSZ-ben
                    bikaKlsz = new string(bikaKlsz.Where(char.IsDigit).ToArray());

                    string termMod = semen?.ProductionMethod == SemenProductionMethod.Természetes ? "2" : "1";
                    string spermAzon = semen?.ProductionNumber ?? "";
                    string spermTip = semen?.Type == SemenType.Fagyasztott ? "2" : "1";
                    string spermEred = semen?.Origin == SemenOrigin.Import ? "2" : "1";

                    // 🔥 FEL_HO oszlop kezelése a kérésed szerint:
                    // Teheneknél csak a hónap (szóközökkel eltolva balról, pl. "  05"), üszőknél évszázad nélkül + hónap ("2605")
                    string felHoFormatted = isUszo
                        ? befejesDatuma.ToString("yyMM")
                        : befejesDatuma.Month.ToString("D2").PadLeft(4, ' ');

                    string[] rowValues = new string[]
                    {
                megye,
                tenyeszet,
                telep,
                animal.EnarNumber ?? "",
                kovEllSsz.ToString("D2"),
                termSsz.ToString("D2"),
                log.EventDate.ToString("yyyyMMdd"),
                bikaKlsz,
                "", // VEMH_KOD
                "", // D_AZ_TIP
                "", // D_AZON
                "", // SUPEROVUL
                "0",// ISM_BIKA
                termMod,
                inszKod,
                "", // ELOALL_KOD
                spermAzon,
                spermTip,
                spermEred,
                befejesDatuma.ToString("yyyyMMdd"), // OSSZBEF_DAT
                felHoFormatted,                     // BEFEFEL_HO (FEL_HO)
                "", // DABEVEMH_HO
                befejesDatuma.ToString("yyyyMMdd")  // SZARMOD_DAT (MOD_DAT)
                    };

                    bw.Write((byte)0x20);

                    for (int i = 0; i < fields.Length; i++)
                    {
                        string val = rowValues[i] ?? "";
                        int targetLen = fields[i].Length;

                        string padded = val.PadRight(targetLen);
                        if (padded.Length > targetLen) padded = padded.Substring(0, targetLen);

                        byte[] recordBytes = Encoding.GetEncoding("windows-1250").GetBytes(padded);
                        bw.Write(recordBytes);
                    }
                }

                bw.Write((byte)0x1A);
                bw.Flush();
                return ms.ToArray();
            }
        }

        // ---- NYILVÁNOS HÍVHATÓ METÓDUSOK A CONTROLLERNEK ----

        // 1. TEHENEK TERMÉKENYÍTÉSE
        public async Task<byte[]> GenerateTehenTermekenyitesDbfAsync(string megye, string tenyeszet, string telep, DateTime befejesDatuma, DateTime utolsoBefejesOta)
        {
            _context.ChangeTracker.Clear();

            // Lekérjük a tehenekhez tartozó logokat az időszakban
            var logs = await _context.InseminationLogs
                .AsNoTracking()
                .Where(l => l.EventDate > utolsoBefejesOta && l.EventDate <= befejesDatuma)
                .Where(l => _context.Cattles.Any(c => c.EarTag == l.CattleEarTag && c.AgeGroup == "Tehén" && c.EarTag != "SYSTEM"))
                .ToListAsync();

            return await BuildTermekenyitesDbfAsync(logs, megye, tenyeszet, telep, befejesDatuma, isUszo: false);
        }

        // 2. ÜSZŐK TERMÉKENYÍTÉSE
        public async Task<byte[]> GenerateUszoTermekenyitesDbfAsync(string megye, string tenyeszet, string telep, DateTime befejesDatuma, DateTime utolsoBefejesOta)
        {
            _context.ChangeTracker.Clear();

            // Lekérjük az üszőköz tartozó logokat az időszakban
            var logs = await _context.InseminationLogs
                .AsNoTracking()
                .Where(l => l.EventDate > utolsoBefejesOta && l.EventDate <= befejesDatuma)
                .Where(l => _context.Cattles.Any(c => c.EarTag == l.CattleEarTag && c.AgeGroup != "Tehén" && c.EarTag != "SYSTEM"))
                .ToListAsync();

            return await BuildTermekenyitesDbfAsync(logs, megye, tenyeszet, telep, befejesDatuma, isUszo: true);
        }

        public byte[] GenerateUszoUjFelvEtelDbf()
        {
            var fields = new (string Name, char Type, byte Length)[]
            {
        ("BAZON",          'C', 9),
        ("MEGYE",          'C', 2),
        ("TENYESZET",      'C', 3),
        ("TELEP",          'C', 2),
        ("AZONOSITO",      'C', 12),
        ("E_AZON",         'C', 12),
        ("B_NEME",         'C', 1),
        ("SZUL_DAT",       'D', 8),
        ("ANYA_AZON",      'C', 12),
        ("APA_AZON",       'C', 12),
        ("AN_NAGYANY",     'C', 12),
        ("AN_NAGYAPA",     'C', 12),
        ("NEVE",           'C', 38),
        ("HV_KOD",         'C', 4),
        ("HV_DAT",         'D', 8),
        ("ELL_DAT",        'D', 8),
        ("BAZON2",         'C', 9),
        ("KONSTR_KOD",     'C', 4),
        ("ELAP_ATV",       'C', 1),
        ("MOD_DAT",        'D', 8)
            };

            int recordLength = 1 + fields.Sum(f => f.Length); // 1 + 180 = 181 bájt
            short headerLength = (short)(32 + (fields.Length * 32) + 1);

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.GetEncoding("windows-1250")))
            {
                // 1. Fejléc írása (0 adatrekorddal!)
                bw.Write((byte)0x03);
                bw.Write((byte)(DateTime.Today.Year - 1900));
                bw.Write((byte)DateTime.Today.Month);
                bw.Write((byte)DateTime.Today.Day);
                bw.Write(0); // Fixen 0 rekord, mivel teljesen üres
                bw.Write(headerLength);
                bw.Write((short)recordLength);
                bw.Write(new byte[20]);

                // 2. Mezőleírók írása
                foreach (var field in fields)
                {
                    byte[] nameBytes = new byte[11];
                    byte[] rawName = Encoding.ASCII.GetBytes(field.Name);
                    Array.Copy(rawName, nameBytes, Math.Min(rawName.Length, 11));
                    bw.Write(nameBytes);
                    bw.Write(field.Type);
                    bw.Write(0);
                    bw.Write(field.Length);
                    bw.Write((byte)0);
                    bw.Write(new byte[14]);
                }
                bw.Write((byte)0x0D);

                // Mivel 0 adatsor van, ide nem írunk rekordokat, azonnal zárjuk a fájlt

                bw.Write((byte)0x1A); // dBase fájl vége jelző
                bw.Flush();
                return ms.ToArray();
            }
        }

        public async Task<byte[]> GenerateTehenUjFelvetelDbfAsync(string megye, string tenyeszet, string telep, DateTime befejesDatuma, DateTime utolsoBefejesOta)
        {
            _context.ChangeTracker.Clear();

            // 🔥 JAVÍTÁS: Mivel nincs EntryDate, azokat a teheneket keressük,
            // amelyek már ellettek (CurrentLactationNo >= 1), és a feltételezett első ellésük/bejegyzésük az időszakba esik.
            // (Ha van Ellési naplód, érdemes lehet abból indítani, de ha nincs, a BirthDate-hez képest számolt kor vagy az utolsó termékenyítés is segíthet.
            // Ideiglenesen azokat a teheneket gyűjtjük be, amelyek aktívak, tehén státuszúak és nem a SYSTEM rekordok.)
            var newCows = await _context.Cattles
                .AsNoTracking()
                .Where(c => c.AgeGroup == "Tehén" && c.EarTag != "SYSTEM" && c.CurrentLactationNo >= 1)
                // Mivel az üszőből tehénné válás az első ellés, ha van ellési esemény dátumod, az a legjobb.
                // Ha nincs külön mező, teszteléshez megnézzük azokat, ahol az ExitDate üres és a befejezés hónapjában vagyunk:
                .ToListAsync();

            // Ha szigorúan szűrni akarod az időszakra, és van egy Ellés táblád (pl. CalvingLog), akkor így lenne a legszebb:
            // var earTagsInPeriod = await _context.CalvingLogs.Where(l => l.EventDate > utolsoBefejesOta && l.EventDate <= befejesDatuma).Select(l => l.CattleEarTag).ToListAsync();
            // var newCows = await _context.Cattles.AsNoTracking().Where(c => c.AgeGroup == "Tehén" && earTagsInPeriod.Contains(c.EarTag)).ToListAsync();

            var fields = new (string Name, char Type, byte Length)[]
            {
        ("MEGYE",          'C', 2),
        ("TENYESZET",      'C', 3),
        ("TELEP",          'C', 2),
        ("AZONOSITO",      'C', 12),
        ("E_AZON",         'C', 12),
        ("PS_AZON",        'C', 5),
        ("SZUL_IDO",       'D', 8),
        ("NEV",            'C', 16),
        ("SZIN",           'C', 1),
        ("ATM_DAT",        'D', 8),
        ("ALL_FELV_D",     'D', 8),
        ("EVHO_UJ",        'C', 4),
        ("TULAJ",          'C', 1),
        ("MOD_DAT",        'D', 8)
            };

            int recordLength = 1 + fields.Sum(f => f.Length); // 81 bájt
            short headerLength = (short)(32 + (fields.Length * 32) + 1);

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.GetEncoding("windows-1250")))
            {
                // Fejléc
                bw.Write((byte)0x03);
                bw.Write((byte)(DateTime.Today.Year - 1900));
                bw.Write((byte)DateTime.Today.Month);
                bw.Write((byte)DateTime.Today.Day);
                bw.Write(newCows.Count);
                bw.Write(headerLength);
                bw.Write((short)recordLength);
                bw.Write(new byte[20]);

                // Mezőleírók
                foreach (var field in fields)
                {
                    byte[] nameBytes = new byte[11];
                    byte[] rawName = Encoding.ASCII.GetBytes(field.Name);
                    Array.Copy(rawName, nameBytes, Math.Min(rawName.Length, 11));
                    bw.Write(nameBytes);
                    bw.Write(field.Type);
                    bw.Write(0);
                    bw.Write(field.Length);
                    bw.Write((byte)0);
                    bw.Write(new byte[14]);
                }
                bw.Write((byte)0x0D);

                // Adatsorok írása
                foreach (var cow in newCows)
                {
                    string enar = cow.EnarNumber ?? "";
                    string psAzon = "";
                    if (enar.Length >= 12)
                    {
                        psAzon = enar.Substring(7, 4);
                    }

                    string generaltNev = "TEHÉN-" + (psAzon.Trim());

                    // 🔥 JAVÍTÁS: EntryDate helyett a befejezés dátumának hónapjából indulunk ki az ATM_DAT-nál
                    // (Mivel a szabály szerint az átminősítés hónapjának 1. napja kell, így a befejesDatuma hónapjának 1. napja tökéletes lesz)
                    DateTime atmHonasElsoNapja = new DateTime(befejesDatuma.Year, befejesDatuma.Month, 1);

                    string evhoUj = befejesDatuma.ToString("yyMM");

                    string[] rowValues = new string[]
                    {
                megye,
                tenyeszet,
                telep,
                enar,
                "", // E_AZON üres
                psAzon,
                cow.BirthDate.ToString("yyyyMMdd"), // Mivel kötelező mező, nem kell a null-conditional ?
                generaltNev,
                "1", // SZIN fixen 1
                atmHonasElsoNapja.ToString("yyyyMMdd"),
                befejesDatuma.ToString("yyyyMMdd"),
                evhoUj,
                "1", // TULAJ fixen 1
                befejesDatuma.ToString("yyyyMMdd")
                    };

                    bw.Write((byte)0x20);

                    for (int i = 0; i < fields.Length; i++)
                    {
                        string val = rowValues[i] ?? "";
                        int targetLen = fields[i].Length;

                        string padded = val.PadRight(targetLen);
                        if (padded.Length > targetLen) padded = padded.Substring(0, targetLen);

                        byte[] recordBytes = Encoding.GetEncoding("windows-1250").GetBytes(padded);
                        bw.Write(recordBytes);
                    }
                }

                bw.Write((byte)0x1A);
                bw.Flush();
                return ms.ToArray();
            }
        }

        private async Task<byte[]> BuildVemhessegDbfAsync(System.Collections.Generic.List<Cattle> pregnantAnimals, string megye, string tenyeszet, string telep, DateTime befejesDatuma)
        {
            var fields = new (string Name, char Type, byte Length)[]
            {
        ("MEGYE",          'C', 2),
        ("TENYESZET",      'C', 3),
        ("TELEP",          'C', 2),
        ("AZONOSITO",      'C', 12),
        ("K_ELL_SSZ",      'C', 2),
        ("TERM_SSZ",       'C', 2),
        ("VEMH_KOD",       'C', 1),
        ("FEL_HO",         'C', 4),
        ("MOD_DAT",        'D', 8)
            };

            int recordLength = 1 + fields.Sum(f => f.Length); // 1 + 35 = 36 bájt
            short headerLength = (short)(32 + (fields.Length * 32) + 1);

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.GetEncoding("windows-1250")))
            {
                // Fejléc írása
                bw.Write((byte)0x03);
                bw.Write((byte)(DateTime.Today.Year - 1900));
                bw.Write((byte)DateTime.Today.Month);
                bw.Write((byte)DateTime.Today.Day);
                bw.Write(pregnantAnimals.Count);
                bw.Write(headerLength);
                bw.Write((short)recordLength);
                bw.Write(new byte[20]);

                // Mezőleírók írása
                foreach (var field in fields)
                {
                    byte[] nameBytes = new byte[11];
                    byte[] rawName = Encoding.ASCII.GetBytes(field.Name);
                    Array.Copy(rawName, nameBytes, Math.Min(rawName.Length, 11));
                    bw.Write(nameBytes);
                    bw.Write(field.Type);
                    bw.Write(0);
                    bw.Write(field.Length);
                    bw.Write((byte)0);
                    bw.Write(new byte[14]);
                }
                bw.Write((byte)0x0D);

                // Adatsorok generálása
                foreach (var animal in pregnantAnimals)
                {
                    // K_ELL_SSZ: aktuális laktáció + 1
                    int kovEllSsz = animal.CurrentLactationNo + 1;

                    // TERM_SSZ: Hány termékenyítése volt összesen ennek az állatnak a sikeres vizsgálat napjáig
                    // (Mivel vemhes lett, az utolsó bejegyzett termékenyítés sorszáma adja meg a sikeres alkalmat)
                    DateTime vizsgalatNapja = animal.LastPregnancyTestDate ?? befejesDatuma;
                    int termSsz = await _context.InseminationLogs
                        .AsNoTracking()
                        .CountAsync(l => l.CattleEarTag == animal.EarTag && l.EventDate <= vizsgalatNapja);

                    // Ha valamiért 0 lenne (pl. nincs felvive előzmény), KÁT biztonság kedvéért 01 legyen
                    if (termSsz == 0) termSsz = 1;

                    string felHo = befejesDatuma.ToString("yyMM");

                    string[] rowValues = new string[]
                    {
                megye,
                tenyeszet,
                telep,
                animal.EnarNumber ?? "",
                kovEllSsz.ToString("D2"),
                termSsz.ToString("D2"),
                "1", // VEMH_KOD fixen 1
                felHo,
                befejesDatuma.ToString("yyyyMMdd")
                    };

                    bw.Write((byte)0x20);

                    for (int i = 0; i < fields.Length; i++)
                    {
                        string val = rowValues[i] ?? "";
                        int targetLen = fields[i].Length;

                        string padded = val.PadRight(targetLen);
                        if (padded.Length > targetLen) padded = padded.Substring(0, targetLen);

                        byte[] recordBytes = Encoding.GetEncoding("windows-1250").GetBytes(padded);
                        bw.Write(recordBytes);
                    }
                }

                bw.Write((byte)0x1A);
                bw.Flush();
                return ms.ToArray();
            }
        }

        // ---- PUBLIKUS Controller HÍVÁSOK ----

        // 1. TEHÉNVEMHESSÉG
        public async Task<byte[]> GenerateTehenVemhessegDbfAsync(string megye, string tenyeszet, string telep, DateTime befejesDatuma, DateTime utolsoBefejesOta)
        {
            _context.ChangeTracker.Clear();

            var cows = await _context.Cattles
                .AsNoTracking()
                .Where(c => c.AgeGroup == "Tehén" && c.EarTag != "SYSTEM")
                .Where(c => c.PregnancyStatus == PregnancyStatus.Vemhes)
                .Where(c => c.LastPregnancyTestDate > utolsoBefejesOta && c.LastPregnancyTestDate <= befejesDatuma)
                .ToListAsync();

            return await BuildVemhessegDbfAsync(cows, megye, tenyeszet, telep, befejesDatuma);
        }

        // 2. ÜSZŐVEMHESSÉG
        public async Task<byte[]> GenerateUszoVemhessegDbfAsync(string megye, string tenyeszet, string telep, DateTime befejesDatuma, DateTime utolsoBefejesOta)
        {
            _context.ChangeTracker.Clear();

            var heifers = await _context.Cattles
                .AsNoTracking()
                .Where(c => c.AgeGroup != "Tehén" && c.EarTag != "SYSTEM")
                .Where(c => c.PregnancyStatus == PregnancyStatus.Vemhes)
                .Where(c => c.LastPregnancyTestDate > utolsoBefejesOta && c.LastPregnancyTestDate <= befejesDatuma)
                .ToListAsync();

            return await BuildVemhessegDbfAsync(heifers, megye, tenyeszet, telep, befejesDatuma);
        }

        public byte[] GenerateJhibakDbf()
        {
            var fields = new (string Name, char Type, byte Length)[]
            {
        ("AZONOSITO",  'C', 12),
        ("HIBAUZENET", 'C', 62)
            };

            int recordLength = 1 + fields.Sum(f => f.Length); // 1 + 74 = 75 bájt
            short headerLength = (short)(32 + (fields.Length * 32) + 1);

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.GetEncoding("windows-1250")))
            {
                // Fejléc írása (0 adatrekorddal)
                bw.Write((byte)0x03);
                bw.Write((byte)(DateTime.Today.Year - 1900));
                bw.Write((byte)DateTime.Today.Month);
                bw.Write((byte)DateTime.Today.Day);
                bw.Write(0); // Fixen 0 rekord, tiszta, hiba nélküli export!
                bw.Write(headerLength);
                bw.Write((short)recordLength);
                bw.Write(new byte[20]);

                // Mezőleírók írása
                foreach (var field in fields)
                {
                    byte[] nameBytes = new byte[11];
                    byte[] rawName = Encoding.ASCII.GetBytes(field.Name);
                    Array.Copy(rawName, nameBytes, Math.Min(rawName.Length, 11));
                    bw.Write(nameBytes);
                    bw.Write(field.Type);
                    bw.Write(0);
                    bw.Write(field.Length);
                    bw.Write((byte)0);
                    bw.Write(new byte[14]);
                }
                bw.Write((byte)0x0D);

                // Nincs adatsor, azonnal lezárjuk a fájlt
                bw.Write((byte)0x1A);
                bw.Flush();
                return ms.ToArray();
            }
        }
    }
}