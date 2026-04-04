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
    public class DeathReasonsController : Controller
    {
        private readonly IzabellaDbContext _context;

        public DeathReasonsController(IzabellaDbContext context)
        {
            _context = context;
        }

        // GET: DeathReasons
        public async Task<IActionResult> Index()
        {
            return View(await _context.DeathReasons.ToListAsync());
        }

        // GET: DeathReasons/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var deathReason = await _context.DeathReasons
                .FirstOrDefaultAsync(m => m.Id == id);
            if (deathReason == null)
            {
                return NotFound();
            }

            return View(deathReason);
        }

        // GET: DeathReasons/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: DeathReasons/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name")] DeathReason deathReason)
        {
            if (ModelState.IsValid)
            {
                _context.Add(deathReason);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(deathReason);
        }

        // GET: DeathReasons/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var deathReason = await _context.DeathReasons.FindAsync(id);
            if (deathReason == null)
            {
                return NotFound();
            }
            return View(deathReason);
        }

        // POST: DeathReasons/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] DeathReason deathReason)
        {
            if (id != deathReason.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(deathReason);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DeathReasonExists(deathReason.Id))
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
            return View(deathReason);
        }

        // GET: DeathReasons/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var deathReason = await _context.DeathReasons
                .FirstOrDefaultAsync(m => m.Id == id);
            if (deathReason == null)
            {
                return NotFound();
            }

            return View(deathReason);
        }

        // POST: DeathReasons/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var deathReason = await _context.DeathReasons.FindAsync(id);
            if (deathReason != null)
            {
                _context.DeathReasons.Remove(deathReason);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DeathReasonExists(int id)
        {
            return _context.DeathReasons.Any(e => e.Id == id);
        }
    }
}
