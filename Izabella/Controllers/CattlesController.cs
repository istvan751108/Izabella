using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Izabella.Models;

namespace Izabella.Controllers
{
    public class CattlesController : Controller
    {
        private readonly IzabellaDbContext _context;

        public CattlesController(IzabellaDbContext context)
        {
            _context = context;
        }

        // GET: Cattles
        public async Task<IActionResult> Index()
        {
            var izabellaDbContext = _context.Cattles.Include(c => c.Company).Include(c => c.CurrentHerd);
            return View(await izabellaDbContext.ToListAsync());
        }

        // GET: Cattles/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cattle = await _context.Cattles
                .Include(c => c.Company)
                .Include(c => c.CurrentHerd)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (cattle == null)
            {
                return NotFound();
            }

            return View(cattle);
        }

        // GET: Cattles/Create
        public IActionResult Create()
        {
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name");
            ViewData["CurrentHerdId"] = new SelectList(_context.Herds, "Id", "HerdCode");
            return View();
        }

        // POST: Cattles/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Cattle cattle)
        {
            // Kényszerítsük ki az érvényességet azokra a mezőkre, amik üresek maradhatnak
            ModelState.Remove("CurrentHerd");
            ModelState.Remove("Company");

            if (ModelState.IsValid)
            {
                _context.Add(cattle);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Ha hiba van, újraépítjük a listákat a nézethez
            ViewBag.CompanyId = new SelectList(_context.Companies, "Id", "Name", cattle.CompanyId);
            ViewBag.CurrentHerdId = new SelectList(_context.Herds, "Id", "Name", cattle.CurrentHerdId);
            return View(cattle);
        }

        // GET: Cattles/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cattle = await _context.Cattles.FindAsync(id);
            if (cattle == null)
            {
                return NotFound();
            }
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name", cattle.CompanyId);
            ViewData["CurrentHerdId"] = new SelectList(_context.Herds, "Id", "HerdCode", cattle.CurrentHerdId);
            return View(cattle);
        }

        // POST: Cattles/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EarTag,EnarNumber,PassportNumber,PassportSequence,CompanyId,CurrentHerdId,AgeGroup,IsTwin,IsAlive,DamAgeAtCalving,BirthDate,BirthWeight,Gender,MotherEnar,FatherKlsz,ExitDate,ExitType,IsActive")] Cattle cattle)
        {
            if (id != cattle.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cattle);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CattleExists(cattle.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name", cattle.CompanyId);
            ViewData["CurrentHerdId"] = new SelectList(_context.Herds, "Id", "HerdCode", cattle.CurrentHerdId);
            return View(cattle);
        }

        // GET: Cattles/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cattle = await _context.Cattles
                .Include(c => c.Company)
                .Include(c => c.CurrentHerd)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (cattle == null)
            {
                return NotFound();
            }

            return View(cattle);
        }

        // POST: Cattles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cattle = await _context.Cattles.FindAsync(id);
            if (cattle != null)
            {
                _context.Cattles.Remove(cattle);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CattleExists(int id)
        {
            return _context.Cattles.Any(e => e.Id == id);
        }
    }
}
