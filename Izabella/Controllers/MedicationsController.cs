using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Izabella.Models;

public class MedicationsController : Controller
{
    private readonly IzabellaDbContext _context;

    public MedicationsController(IzabellaDbContext context)
    {
        _context = context;
    }

    // GET: MEDICATIONS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Medications.ToListAsync());
    }

    // GET: MEDICATIONS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var medication = await _context.Medications
            .FirstOrDefaultAsync(m => m.Id == id);
        if (medication == null)
        {
            return NotFound();
        }

        return View(medication);
    }

    // GET: MEDICATIONS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: MEDICATIONS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name,Quantity,Unit,DefaultDose,IsAntibiotic,Category,WithdrawalPeriodMilk,WithdrawalPeriodMeat")] Medication medication)
    {
        if (ModelState.IsValid)
        {
            _context.Add(medication);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(medication);
    }

    // GET: MEDICATIONS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var medication = await _context.Medications.FindAsync(id);
        if (medication == null)
        {
            return NotFound();
        }
        return View(medication);
    }

    // POST: MEDICATIONS/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Name,Quantity,Unit,DefaultDose,IsAntibiotic,Category,WithdrawalPeriodMilk,WithdrawalPeriodMeat")] Medication medication)
    {
        if (id != medication.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(medication);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MedicationExists(medication.Id))
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
        return View(medication);
    }

    // GET: MEDICATIONS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var medication = await _context.Medications
            .FirstOrDefaultAsync(m => m.Id == id);
        if (medication == null)
        {
            return NotFound();
        }

        return View(medication);
    }

    // POST: MEDICATIONS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var medication = await _context.Medications.FindAsync(id);
        if (medication != null)
        {
            _context.Medications.Remove(medication);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool MedicationExists(int? id)
    {
        return _context.Medications.Any(e => e.Id == id);
    }
}
