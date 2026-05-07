using Izabella.Models;

namespace Izabella.Models.ViewModels
{
    public class OffspringLogVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int? SelectedCompanyId { get; set; }
        public List<Company> Companies { get; set; } = new();
        public List<OffspringEntry> Entries { get; set; } = new();
    }

    public class OffspringEntry
    {
        public DateTime BirthDate { get; set; }
        // Borjú adatai
        public string CalfEarTag { get; set; }
        public string CalfEnar { get; set; }
        public Gender CalfGender { get; set; }
        public double BirthWeight { get; set; }

        // Anya adatai
        public string MotherEnar { get; set; }
        public string MotherEarTag { get; set; } // Kikeresve az ENAR alapján
    }
}
