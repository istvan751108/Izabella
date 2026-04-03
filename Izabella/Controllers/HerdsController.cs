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
    public class HerdsController : Controller
    {
        private readonly IzabellaDbContext _context;

        public HerdsController(IzabellaDbContext context)
        {
            _context = context;
        }

        // GET: Herds
        public async Task<IActionResult> Index()
        {
            var izabellaDbContext = _context.Herds.Include(h => h.Company);
            return View(await izabellaDbContext.ToListAsync());
        }

        // GET: Herds/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var herd = await _context.Herds
                .Include(h => h.Company)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (herd == null)
            {
                return NotFound();
            }

            return View(herd);
        }

        // GET: Herds/Create
        public IActionResult Create()
        {
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name");
            return View();
        }

        // POST: Herds/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,HerdCode,CompanyId,DefaultPrefix,EnarPrefix")] Herd herd)
        {
            if (ModelState.IsValid)
            {
                _context.Add(herd);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name");
            return View(herd);
        }

        // GET: Herds/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var herd = await _context.Herds.FindAsync(id);
            if (herd == null)
            {
                return NotFound();
            }
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name");
            return View(herd);
        }

        // POST: Herds/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,HerdCode,CompanyId,DefaultPrefix,EnarPrefix")] Herd herd)
        {
            if (id != herd.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(herd);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!HerdExists(herd.Id))
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
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "Name", herd.CompanyId);
            return View(herd);
        }

        // GET: Herds/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var herd = await _context.Herds
                .Include(h => h.Company)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (herd == null)
            {
                return NotFound();
            }

            return View(herd);
        }

        // POST: Herds/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var herd = await _context.Herds.FindAsync(id);
            if (herd != null)
            {
                _context.Herds.Remove(herd);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool HerdExists(int id)
        {
            return _context.Herds.Any(e => e.Id == id);
        }
    }
}
