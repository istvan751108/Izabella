using Microsoft.AspNetCore.Mvc;

namespace Izabella.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View(); // Ez fogja betölteni a Views/Home/Index.cshtml-t
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}

