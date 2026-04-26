using Izabella.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Controllers
{
    public class StaffController : Controller
    {
        private readonly IzabellaDbContext _context;
        public StaffController(IzabellaDbContext context) { _context = context; }

        public async Task<IActionResult> Index()
        {
            var staff = await _context.Staffs.OrderBy(s => s.Role).ThenBy(s => s.Name).ToListAsync();
            return View(staff);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Staff person)
        {
            if (ModelState.IsValid)
            {
                _context.Add(person);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{person.Name} hozzáadva a listához.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var person = await _context.Staffs.FindAsync(id);
            if (person != null)
            {
                person.IsActive = !person.IsActive;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var person = await _context.Staffs.FindAsync(id);
            if (person != null)
            {
                _context.Staffs.Remove(person);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}