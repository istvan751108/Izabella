namespace Izabella.Models.ViewModels
{
    // Egyszerű ViewModel a felülethez
    public class MilkDistributionVM
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public double Kg { get; set; }
        public double Liter { get; set; }
    }
}