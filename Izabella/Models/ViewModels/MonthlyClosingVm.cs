namespace Izabella.Models.ViewModels
{
    public class MonthlyClosingVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<Company> Companies { get; set; } = new List<Company>();

        // Értékesítés és Elhullás adatok: Korcsoport -> Cég -> Típus -> Darabszám
        public Dictionary<string, Dictionary<int, Dictionary<string, int>>> SalesData { get; set; } = new();
        public Dictionary<string, Dictionary<int, int>> DeathData { get; set; } = new();

        // Ellések: AnyaKorcsoport (Tehén/Üsző) -> Típus (Sima, Iker, Hulla) -> Darabszám
        public Dictionary<string, Dictionary<string, int>> CalvingData { get; set; } = new();

        // Szaporulat: Nem -> Darabszám
        public Dictionary<string, int> OffspringData { get; set; } = new();

        // Állomány: Cég -> Korcsoport (Tehén/Növendék) -> Darabszám
        public Dictionary<int, Dictionary<string, int>> InventoryData { get; set; } = new();

        // Termékenyítés: AnyaKorcsoport -> Darabszám
        public Dictionary<string, int> InseminationData { get; set; } = new();
        // Honnan (CégId) -> Hová (CégId) -> Darabszám
        public Dictionary<int, Dictionary<int, int>> TransferData { get; set; } = new();
        public int? SelectedCompanyId { get; set; }

        public List<string> AgeGroups => new() {
        "Itatásos borjú", "Borjú", "Növendék 6-9", "Növendék 9-12",
        "Növendék 12 hó-tól", "Vemhes üsző", "Tehén"
        };
    }
}
