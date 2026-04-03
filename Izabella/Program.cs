using Izabella.Models;
using Izabella.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
var builder = WebApplication.CreateBuilder(args);

// 1. Identity konfigurálása
builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddRoles<IdentityRole>() // Fontos: Szerepkörök bekapcsolása!
.AddEntityFrameworkStores<IzabellaDbContext>();

// Adatbázis kontextus regisztrálása a DI konténerbe
builder.Services.AddDbContext<IzabellaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();
builder.Services.AddMvc().AddDataAnnotationsLocalization();
builder.Services.AddScoped<VoucherService>();
builder.Services.AddScoped<ManureCalculationService>();
builder.Services.AddScoped<SolidManureService>();
var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();

// 2. Magyar nyelv beállítása (Localization)
var supportedCultures = new[] { new CultureInfo("hu-HU") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("hu-HU"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseAuthentication(); // Ki azonosította magát?
app.UseAuthorization();  // Mit szabad csinálnia?

app.MapStaticAssets();
app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
// Configure the HTTP request pipeline.
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
// 3. Admin user létrehozása (Seed)
using (var scope = app.Services.CreateScope())
{
    await SeedData.Initialize(scope.ServiceProvider);
}

app.Run();
