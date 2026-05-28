using Izabella.Models;
using Izabella.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Izabella.Controllers
{
    public class MilkAnalysisController : Controller
    {
        private readonly MilkAnalysisService _analysisService;
        private readonly EmailSenderService _emailSender;
        private readonly IzabellaDbContext _context;

        public MilkAnalysisController(MilkAnalysisService analysisService, EmailSenderService emailSender, IzabellaDbContext context)
        {
            _analysisService = analysisService;
            _emailSender = emailSender;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.Companies = await _context.Companies.AsNoTracking().ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ExportAndEmail(int year, int? month, int companyId, string feedCompanyEmail, bool sendEmail)
        {
            var reportData = await _analysisService.GenerateReportDataAsync(year, month, companyId);
            byte[] excelBytes = _analysisService.ExportToExcelBytes(reportData);

            string fileName = $"Probafejes_Adatosszefugges_{reportData.CompanyName}_{reportData.PeriodText.Replace(".", "_")}.xlsx";

            if (sendEmail && !string.IsNullOrEmpty(feedCompanyEmail))
            {
                string subject = $"Próbafejés Adatösszefüggés Elemzés - {reportData.CompanyName} ({reportData.PeriodText})";
                string body = $@"
                    <h3>Tisztelt Takarmányozási Szakértő!</h3>
                    <p>Mellékelten megküldjük a(z) <strong>{reportData.CompanyName}</strong> tehenészetének próbafejési adatösszefüggés elemzéseit az alábbi időszakra: {reportData.PeriodText}.</p>
                    <p>A fájl tartalmazza a Tejelő nap és a Tej kg kategória szerinti lebontásokat.</p>
                    <br/>
                    <p>Üdvözlettel,<br/><em>Izabella Nyilvántartó Rendszer</em></p>";

                try
                {
                    await _emailSender.SendReportWithAttachmentAsync(feedCompanyEmail, subject, body, excelBytes, fileName);
                    TempData["SuccessMessage"] = "Az elemzés sikeresen elkészült és elküldésre került e-mailben!";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Az Excel elkészült, de az e-mail küldés sikertelen volt: {ex.Message}";
                }

                return RedirectToAction("Index");
            }

            // Ha nem e-mailt kért, hanem közvetlen letöltést böngészőből
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}