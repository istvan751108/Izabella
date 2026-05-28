using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Izabella.Models;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Services
{
    public class MilkLabImportService
    {
        private readonly IzabellaDbContext _context;

        public MilkLabImportService(IzabellaDbContext context)
        {
            _context = context;
        }

        public async Task<(int SuccessCount, List<string> Errors)> ImportXmlAsync(Stream xmlStream)
        {
            var errors = new List<string>();
            int successCount = 0;

            try
            {
                var doc = XDocument.Load(xmlStream);

                // Az összes <ROW> elem kiválasztása a <ROWDATA> ágból
                var rows = doc.Descendants("ROW").ToList();

                if (!rows.Any())
                {
                    errors.Add("Az XML fájl nem tartalmaz feldolgozható <ROW> rekordokat.");
                    return (0, errors);
                }

                // Gyorsítótárba gyűjtjük az aktív állatokat ENAR alapján az N+1 lekérdezések elkerülésére
                var cattleCache = await _context.Cattles
                    .AsNoTracking()
                    .ToDictionaryAsync(c => c.EnarNumber.Trim(), c => c.Id);

                var resultsToSave = new List<MilkLabResult>();

                foreach (var row in rows)
                {
                    string rawAzonosito = row.Attribute("azonosito")?.Value ?? "";
                    string cleanEnar = rawAzonosito.Trim(); // Levágjuk a felesleges szóközöket

                    if (string.IsNullOrEmpty(cleanEnar))
                    {
                        continue; // Ha nincs azonosító, ugorjunk
                    }

                    // Megkeressük a cache-ben az állatot
                    if (!cattleCache.TryGetValue(cleanEnar, out int cattleId))
                    {
                        errors.Add($"A(z) {cleanEnar} ENAR számú állat nem található a rendszerben. A sor kihagyásra került.");
                        continue;
                    }

                    try
                    {
                        var result = new MilkLabResult
                        {
                            CattleId = cattleId,
                            Megye = row.Attribute("megye")?.Value ?? "",
                            Tenyeszet = row.Attribute("tenyeszet")?.Value ?? "",
                            Telep = row.Attribute("telep")?.Value ?? "",

                            // Dátumok konvertálása
                            BefDat = ParseXmlDate(row.Attribute("bef_dat")?.Value) ?? DateTime.Today,
                            BefDatTol = ParseXmlDate(row.Attribute("bef_dat_tol")?.Value),
                            ModDat = ParseXmlDate(row.Attribute("mod_dat")?.Value) ?? DateTime.Today,

                            AllKod = row.Attribute("all_kod")?.Value ?? "",
                            EllMod = row.Attribute("ell_mod")?.Value,
                            AzTipus = row.Attribute("AZ_tipus")?.Value,

                            // Összesített eredmények
                            NapiTej = ParseXmlDouble(row.Attribute("Napi_tej")?.Value),
                            NapiZsir = ParseXmlDouble(row.Attribute("Napi_zsir")?.Value),
                            NapiFeherje = ParseXmlDouble(row.Attribute("Napi_feh")?.Value),
                            SzomatikusSejtszam = ParseXmlInt(row.Attribute("szomat")?.Value),
                            Karbamid = ParseXmlDouble(row.Attribute("karbamid")?.Value),

                            // 1. Fejés
                            Tej1 = ParseXmlDouble(row.Attribute("tej_1")?.Value),
                            Zsir1 = ParseXmlDouble(row.Attribute("zsir_1")?.Value),
                            Feherje1 = ParseXmlDouble(row.Attribute("feh_1")?.Value),
                            Cukor1 = ParseXmlDouble(row.Attribute("cuk_1")?.Value),
                            Vonalkod1 = row.Attribute("vonalkod_1")?.Value?.Trim(),
                            Idopont1 = ParseXmlDate(row.Attribute("idopont_1")?.Value),

                            // 2. Fejés
                            Tej2 = ParseXmlDouble(row.Attribute("tej_2")?.Value),
                            Zsir2 = ParseXmlDouble(row.Attribute("zsir_2")?.Value),
                            Feherje2 = ParseXmlDouble(row.Attribute("feh_2")?.Value),
                            Cukor2 = ParseXmlDouble(row.Attribute("cuk_2")?.Value),
                            Vonalkod2 = row.Attribute("vonalkod_2")?.Value?.Trim(),
                            Idopont2 = ParseXmlDate(row.Attribute("idopont_2")?.Value),

                            // 3. Fejés
                            Tej3 = ParseXmlDouble(row.Attribute("tej_3")?.Value),
                            Zsir3 = ParseXmlDouble(row.Attribute("zsir_3")?.Value),
                            Feherje3 = ParseXmlDouble(row.Attribute("feh_3")?.Value),
                            Cukor3 = ParseXmlDouble(row.Attribute("cuk_3")?.Value),
                            Vonalkod3 = row.Attribute("vonalkod_3")?.Value?.Trim(),
                            Idopont3 = ParseXmlDate(row.Attribute("idopont_3")?.Value),

                            // 4. Fejés
                            Tej4 = ParseXmlDouble(row.Attribute("tej_4")?.Value),
                            Zsir4 = ParseXmlDouble(row.Attribute("zsir_4")?.Value),
                            Feherje4 = ParseXmlDouble(row.Attribute("feh_4")?.Value),
                            Cukor4 = ParseXmlDouble(row.Attribute("cuk_4")?.Value),
                            Vonalkod4 = row.Attribute("vonalkod_4")?.Value?.Trim(),
                            Idopont4 = ParseXmlDate(row.Attribute("idopont_4")?.Value),

                            // 5. Fejés
                            Tej5 = ParseXmlDouble(row.Attribute("tej_5")?.Value),
                            Zsir5 = ParseXmlDouble(row.Attribute("zsir_5")?.Value),
                            Feherje5 = ParseXmlDouble(row.Attribute("feh_5")?.Value),
                            Cukor5 = ParseXmlDouble(row.Attribute("cuk_5")?.Value),
                            Vonalkod5 = row.Attribute("vonalkod_5")?.Value?.Trim(),
                            Idopont5 = ParseXmlDate(row.Attribute("idopont_5")?.Value),

                            // 6. Fejés
                            Tej6 = ParseXmlDouble(row.Attribute("tej_6")?.Value),
                            Zsir6 = ParseXmlDouble(row.Attribute("zsir_6")?.Value),
                            Feherje6 = ParseXmlDouble(row.Attribute("feh_6")?.Value),
                            Cukor6 = ParseXmlDouble(row.Attribute("cuk_6")?.Value),
                            Vonalkod6 = row.Attribute("vonalkod_6")?.Value?.Trim(),
                            Idopont6 = ParseXmlDate(row.Attribute("idopont_6")?.Value),

                            ImportedAt = DateTime.Now
                        };

                        resultsToSave.Add(result);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Hiba a(z) {cleanEnar} állat adatainak feldolgozásakor: {ex.Message}");
                    }
                }

                if (resultsToSave.Any())
                {
                    await _context.MilkLabResults.AddRangeAsync(resultsToSave);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception globalEx)
            {
                errors.Add($"Súlyos hiba történt az XML parse-olása közben: {globalEx.Message}");
            }

            return (successCount, errors);
        }

        // --- Kisegítő típuskonvertálók ---
        private double ParseXmlDouble(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0.0;
            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double res) ? res : 0.0;
        }

        private int ParseXmlInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            return int.TryParse(value, out int res) ? res : 0;
        }

        private DateTime? ParseXmlDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            // Ha Delphi ISO formátumban van (pl. 20260511 vagy 20260511T13:46:00000)
            if (value.Contains("T"))
            {
                string cleanDate = value.Split('T')[0];
                if (DateTime.TryParseExact(cleanDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime res))
                    return res;
            }

            if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime directRes))
                return directRes;

            return DateTime.TryParse(value, out DateTime fallback) ? fallback : null;
        }
    }
}