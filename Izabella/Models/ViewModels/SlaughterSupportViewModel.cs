namespace Izabella.Models.ViewModels
{
    public class SlaughterSupportViewModel
    {
        public string DestinationName { get; set; } // Rendeltetési hely (Vevő)
        public int Count0to6 { get; set; }          // 0-6 hónap
        public int Count6to24 { get; set; }         // 6-24 hónap
        public int CountOver24 { get; set; }        // 24 hónap felett
        public int Total => Count0to6 + Count6to24 + CountOver24;
    }
}
