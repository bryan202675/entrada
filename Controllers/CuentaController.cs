using System.Security.Claims;
using Entrada.Data;
using Entrada.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Entrada.Controllers;

[Route("cuenta")]
public sealed class CuentaController : Controller
{
    private readonly TicketingRepository _repository;
    private readonly IPasswordHasher<UsuarioAutenticacion> _passwordHasher;
    private readonly ILogger<CuentaController> _logger;

    public CuentaController(TicketingRepository repository, IPasswordHasher<UsuarioAutenticacion> passwordHasher, ILogger<CuentaController> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("registro")]
    public IActionResult Registro() => View(new RegistroInput());

    [AllowAnonymous]
    [HttpPost("registro")]
    public async Task<IActionResult> Registro(RegistroInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var temporal = new UsuarioAutenticacion(0, input.Nombre.Trim(), input.Email.Trim(), string.Empty, "Usuario");
        var hash = _passwordHasher.HashPassword(temporal, input.Password);
        OperacionResultado resultado;
        try
        {
            resultado = await _repository.CrearUsuarioAsync(input.Nombre, input.Email, hash, cancellationToken);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "No fue posible registrar la cuenta");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ModelState.AddModelError(string.Empty, "El servicio de cuentas no está disponible. Verifica que SQL Server esté iniciado e inténtalo de nuevo.");
            return View(input);
        }
        if (!resultado.Exitoso)
        {
            ModelState.AddModelError(nameof(input.Email), resultado.Mensaje);
            return View(input);
        }

        var usuario = temporal with { Id = checked((int)resultado.Id!.Value), PasswordHash = hash };
        await IniciarSesionAsync(usuario);
        TempData["Mensaje"] = "Tu cuenta está lista.";
        return RedirectToAction("Index", "Inicio");
    }

    [AllowAnonymous]
    [HttpGet("iniciar-sesion")]
    public IActionResult Login(string? returnUrl = null) => View(new LoginInput { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost("iniciar-sesion")]
    public async Task<IActionResult> Login(LoginInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        UsuarioAutenticacion? usuario;
        try
        {
            usuario = await _repository.ObtenerUsuarioPorEmailAsync(input.Email, cancellationToken);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "No fue posible iniciar sesión");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ModelState.AddModelError(string.Empty, "El servicio de cuentas no está disponible. Verifica que SQL Server esté iniciado e inténtalo de nuevo.");
            return View(input);
        }
        if (usuario is null || _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "El correo o la contraseña no son correctos.");
            return View(input);
        }

        await IniciarSesionAsync(usuario);
        if (!string.IsNullOrWhiteSpace(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl))
        {
            return LocalRedirect(input.ReturnUrl);
        }
        return RedirectToAction("Index", "Inicio");
    }

    [Authorize]
    [HttpPost("cerrar-sesion")]
    public async Task<IActionResult> Salir()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Inicio");
    }

    [Authorize]
    [HttpGet("mis-entradas")]
    public async Task<IActionResult> MisEntradas(CancellationToken cancellationToken)
    {
        if (User.IsInRole("Administrador"))
        {
            return RedirectToAction("Index", "Admin");
        }
        var reservas = await _repository.ObtenerReservasUsuarioAsync(UsuarioId(), cancellationToken);
        return View(reservas);
    }

    [AllowAnonymous]
    [HttpGet("acceso-denegado")]
    public IActionResult AccesoDenegado()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private int UsuarioId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task IniciarSesionAsync(UsuarioAutenticacion usuario)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nombre),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Role, usuario.Rol)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
