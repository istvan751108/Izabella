using Izabella.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public class TransportReceiptDocument : IDocument
{
    private List<DeathLog> NormalDeaths { get; }
    private List<DeathLog> StillBorns { get; }
    private List<DeathLog> PendingPassports { get; }
    private DateTime TransportDate { get; }

    public TransportReceiptDocument(List<DeathLog> normal, List<DeathLog> still, List<DeathLog> pending, DateTime date)
    {
        NormalDeaths = normal;
        StillBorns = still;
        PendingPassports = pending;
        TransportDate = date;
    }

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Verdana));

            page.Header().Column(col =>
            {
                col.Item().Text("A vastagon kiemelt adatokat szarvasmarha elszállításakor feltétlen adja meg!").FontSize(12).Bold();
                col.Item().Text("Kizárólag a tényleges elszállítási napon megadott ENAR azonosítójú szarvasmarha kerül kijelentésre a marhalevél átadásakor!").FontSize(9);
                col.Item().PaddingTop(5).AlignCenter().Text($"Dátum: {TransportDate:yyyy.MM.dd}").FontSize(11);
            });

            page.Content().Column(col =>
            {
                // FŐ TÁBLÁZAT
                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(60);
                        for (int i = 0; i < 10; i++) columns.ConstantColumn(22);
                        columns.ConstantColumn(50); columns.ConstantColumn(50);
                        columns.ConstantColumn(40); columns.ConstantColumn(40);
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().RowSpan(2).Element(CellStyle).Text("Ország azonosító betűjel").FontSize(7);
                        header.Cell().ColumnSpan(10).Element(CellStyle).AlignCenter().Text("10 jegyű ENAR azonosító (fülszám)").Bold();
                        header.Cell().ColumnSpan(2).Element(CellStyle).AlignCenter().Text("Marhalevél igazolólap (X-el jelölje)").FontSize(7);
                        header.Cell().ColumnSpan(2).Element(CellStyle).AlignCenter().Text("Súly (kg)").Bold();
                        header.Cell().RowSpan(2).Element(CellStyle).Text("Megjegyzés");
                        header.Cell().Row(2).Column(12).Element(CellStyle).Text("Szállításkor átadva").FontSize(6);
                        header.Cell().Row(2).Column(13).Element(CellStyle).Text("Később küldi").FontSize(6);
                        header.Cell().Row(2).Column(14).Element(CellStyle).Text("Mért").FontSize(6);
                        header.Cell().Row(2).Column(15).Element(CellStyle).Text("Becsült").FontSize(6);
                    });

                    // Keressük meg a fő táblázat ciklusát:
                    foreach (var log in NormalDeaths)
                    {
                        // BIZTONSÁGI SZŰRŐ: Ha az ENAR nem szám (pl. "HALVA-SZÜLETETT"), 
                        // akkor ne írjuk a táblázatba, mert szétcsúszik!
                        string rawEnar = log.EnarNumberAtDeath?.Replace("HU", "").Trim() ?? "";

                        // Ha a maradék nem számokból áll, ugorjuk át (az összesítőben ott lesz)
                        if (!long.TryParse(rawEnar.Replace("-", ""), out _)) continue;

                        char[] digits = rawEnar.PadRight(10, ' ').ToCharArray();

                        table.Cell().Element(CellStyle).Text("HU");
                        foreach (var d in digits) table.Cell().Element(CellStyle).Text(d.ToString()).Bold();

                        // Marhalevél X-elés
                        bool hasPassport = (log.Cattle?.PassportNumber != "Nincs" && log.Cattle?.PassportNumber != "Kérve");
                        table.Cell().Element(CellStyle).Text(hasPassport ? "X" : "");
                        table.Cell().Element(CellStyle).Text(!hasPassport ? "X" : "");

                        table.Cell().Element(CellStyle).Text(""); // Mért
                        table.Cell().Element(CellStyle).Text(log.EstimatedWeight.ToString()).Bold(); // Becsült
                        table.Cell().Element(CellStyle).Text(log.Reason);
                    }

                    for (int i = 0; i < (12 - NormalDeaths.Count); i++)
                        for (int j = 0; j < 16; j++) table.Cell().Element(CellStyle).Height(18).Text(" ");
                });

                // HULLAELLÉS ÉS ÖSSZESÍTÉS
                col.Item().PaddingVertical(5).Row(row =>
                {
                    row.RelativeItem().Column(c => {
                        if (StillBorns.Any())
                            c.Item().Text($"+ {StillBorns.Count} hullaellés (Becsült súly: {StillBorns.Sum(s => s.EstimatedWeight)} kg)").Italic();
                    });
                    row.RelativeItem().AlignRight().Text(x => {
                        x.Span("Összes becsült súly: ").Bold();
                        x.Span($"{NormalDeaths.Sum(n => n.EstimatedWeight) + StillBorns.Sum(s => s.EstimatedWeight)} kg").Bold().FontSize(11);
                    });
                });

                // ALSÓ RÉSZ
                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem(1.2f).Column(leftCol =>
                    {
                        leftCol.Item().Text("Utólag átadott marhalevél igazolólap").FontSize(8).Bold();
                        leftCol.Item().Table(smTable =>
                        {
                            smTable.ColumnsDefinition(c => { c.ConstantColumn(30); for (int i = 0; i < 10; i++) c.ConstantColumn(15); });
                            smTable.Header(h => { h.Cell().Element(CellStyle).Text("betűjel").FontSize(7); h.Cell().ColumnSpan(10).Element(CellStyle).Text("10 jegyű ENAR azonosító").FontSize(7); });

                            // Ide kerülnek az elmaradások
                            foreach (var p in PendingPassports)
                            {
                                string enar = p.EnarNumberAtDeath?.Replace("HU", "").Trim() ?? "";
                                char[] d = enar.PadRight(10, ' ').ToCharArray();
                                smTable.Cell().Element(CellStyle).Text("HU");
                                foreach (var digit in d) smTable.Cell().Element(CellStyle).Text(digit.ToString());
                            }
                            // Üres sorok kitöltése (össz 3 sor)
                            for (int i = 0; i < Math.Max(0, 3 - PendingPassports.Count); i++)
                                for (int j = 0; j < 11; j++) smTable.Cell().Element(CellStyle).Height(15).Text(" ");
                        });
                    });

                    row.ConstantItem(40);

                    row.RelativeItem().PaddingTop(10).Column(rightCol =>
                    {
                        rightCol.Spacing(15);
                        rightCol.Item().BorderBottom(0.5f).PaddingBottom(2).Text("Átadó aláírása:").FontSize(8);
                        rightCol.Item().BorderBottom(0.5f).PaddingBottom(2).Text("Átvevő gépkocsivezető aláírása:").FontSize(8);
                        rightCol.Item().BorderBottom(0.5f).PaddingBottom(2).Text("Rögzítő aláírása:").FontSize(8);
                    });
                });
            });
        });
    }

    static IContainer CellStyle(IContainer container) => container.Border(0.5f).BorderColor(Colors.Black).AlignCenter().AlignMiddle();
}