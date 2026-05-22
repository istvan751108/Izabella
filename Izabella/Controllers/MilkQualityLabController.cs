using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Izabella.Controllers
{
    public class MilkQualityLabController : Controller
    {
        private readonly IzabellaDbContext _context;

        public MilkQualityLabController(IzabellaDbContext context)
        {
            _context = context;
        }

        // Listázás
        public async Task<IActionResult> Index()
        {
            var data = await _context.MilkQualityLabs.OrderByDescending(l => l.RecordDate).ToListAsync();
            return View(data);
        }

        // Létrehozás (GET)
        // GET: MilkQualityLab/Create?returnMonth=2026-05
        public IActionResult Create(string returnMonth)
        {
            ViewBag.ReturnMonth = returnMonth;
            return View(new MilkQualityLab { RecordDate = DateTime.Today });
        }

        // POST: MilkQualityLab/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MilkQualityLab lab, string returnMonth)
        {
            // Megtisztítjuk a validációt a külső és számított mezőktől, hogy biztosan mentsen
            ModelState.Remove("returnMonth");
            ModelState.Remove("DekadNumber");

            if (ModelState.IsValid)
            {
                lab.DekadNumber = lab.RecordDate.Day <= 10 ? 1 : (lab.RecordDate.Day <= 20 ? 2 : 3);

                _context.Add(lab);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Laboradatok sikeresen elmentve.";

                if (!string.IsNullOrEmpty(returnMonth))
                {
                    return RedirectToAction("MonthlyStatement", "MilkReport", new { monthFilter = returnMonth });
                }
                return RedirectToAction("Index", "MilkSale");
            }

            ViewBag.ReturnMonth = returnMonth;
            return View(lab);
        }
    }
}