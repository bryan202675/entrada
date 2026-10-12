using System.Globalization;
using Entrada.Data;
using Entrada.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/cuenta/iniciar-sesion";
        options.AccessDeniedPath = "/cuenta/acceso-denegado";
        options.Cookie.Name = "Entrada.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddScoped<TicketingRepository>();
builder.Services.AddScoped<IPasswordHasher<UsuarioAutenticacion>, PasswordHasher<UsuarioAutenticacion>>();
builder.Services.AddHostedService<AdminBootstrapService>();

var app = builder.Build();

app.UseExceptionHandler("/inicio/error");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

var cultura = new CultureInfo("es-SV");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inicio}/{action=Index}/{id?}");

app.Run();

public partial class Program;
