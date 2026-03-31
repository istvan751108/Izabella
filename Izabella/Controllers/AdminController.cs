using Izabella.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Izabella.Controllers
{
    [Authorize(Roles = "Admin")] // Szigorú védelem!
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // Felhasználók listázása
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<UserVm>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserVm
                {
                    Id = user.Id,
                    Email = user.Email,
                    Role = roles.FirstOrDefault() ?? "Nincs szerepkör"
                });
            }

            return View(userList);
        }

        // Új felhasználó létrehozása (Post)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(UserVm model)
        {
            // Csak az Email és Password meglétét ellenőrizzük manuálisan, 
            // ha a ModelState.IsValid valamiért False lenne más mezők miatt
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
            {
                TempData["Error"] = "Email és jelszó megadása kötelező!";
                return RedirectToAction(nameof(Users));
            }

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Szerepkör hozzáadása
                if (!string.IsNullOrEmpty(model.Role))
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                TempData["Success"] = "Felhasználó sikeresen létrehozva!";
            }
            else
            {
                // Itt fogod látni, ha pl. gyenge a jelszó (Identity hiba)
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Users));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && user.Email != User.Identity.Name) // Magát ne törölhesse
            {
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    TempData["Success"] = "Felhasználó törölve!";
                }
                else
                {
                    TempData["Error"] = "Hiba történt a törlés során.";
                }
            }
            return RedirectToAction(nameof(Users));
        }
        private async Task<List<UserVm>> GetUserList()
        {
            var users = await _userManager.Users.ToListAsync();
            var list = new List<UserVm>();
            foreach (var u in users)
            {
                var r = await _userManager.GetRolesAsync(u);
                list.Add(new UserVm { Id = u.Id, Email = u.Email, Role = r.FirstOrDefault() });
            }
            return list;
        }
    }
}