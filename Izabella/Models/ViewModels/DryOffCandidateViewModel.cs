namespace Izabella.Models.ViewModels
{
    public class DryOffCandidateViewModel
    {
        public Cattle Cattle { get; set; }
        public DateTime ExpectedCalving { get; set; }
        public int DaysUntilCalving { get; set; }
    }

    public class DryOffActionViewModel
    {
        // A listázáshoz
        public List<DryOffCandidateViewModel> Candidates { get; set; } = new();

        // A rögzítéshez (Form adatok)
        public List<int> SelectedCattleIds { get; set; } = new();

        public List<int> SelectedMedicationIds { get; set; } = new(); // Több gyógyszer is kijelölhető
        public DateTime DryOffDate { get; set; } = DateTime.Today;
    }
}