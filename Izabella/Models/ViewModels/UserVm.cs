namespace Izabella.Models.ViewModels
{
    public class UserVm
    {
        public string? Id { get; set; } // Opcionális

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = "User";

        public string Password { get; set; } = string.Empty;

        public string? ConfirmPassword { get; set; } // Opcionális
    }
}