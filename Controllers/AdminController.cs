using Entrada.Data;
using Entrada.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Entrada.Controllers;

[Authorize(Roles = "Administrador")]
[Route("admin")]
public sealed class AdminController : Controller
{
    private readonly TicketingRepository _repository;

    public AdminController(TicketingRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int? editar, CancellationToken cancellationToken)
    {
        var resumen = await _repository.ObtenerDashboardAsync(cancellationToken);
        var boletosDetalle = await _repository.ObtenerBoletosDetalleAdminAsync(cancellationToken);
        var formulario = editar.HasValue
            ? CrearFormulario(resumen.EventosActivos.SingleOrDefault(x => x.Id == editar.Value))
            : new EventoInput { Fecha = DateTime.Now.AddDays(30) };
        return View(new AdminDashboardViewModel { Resumen = resumen, Formulario = formulario, BoletosDetalle = boletosDetalle });
    }

    [HttpPost("eventos/guardar")]
    public async Task<IActionResult> GuardarEvento(EventoInput input, CancellationToken cancellationToken)
    {
        var imagenFinal = string.IsNullOrWhiteSpace(input.Imagen) ? "/images/evento-arte.jpg" : input.Imagen;
        if (input.ArchivoImagen != null && input.ArchivoImagen.Length > 0)
        {
            var extension = Path.GetExtension(input.ArchivoImagen.FileName).ToLowerInvariant();
            var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!extensionesPermitidas.Contains(extension))
            {
                ModelState.AddModelError(nameof(input.ArchivoImagen), "Formato de archivo no válido. Usa JPG, PNG o WebP.");
            }
            else if (input.ArchivoImagen.Length > 10 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(input.ArchivoImagen), "La imagen no debe superar los 10 MB.");
            }
            else
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "uploads");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"evento-{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await input.ArchivoImagen.CopyToAsync(stream, cancellationToken);
                }
                imagenFinal = $"/images/uploads/{fileName}";
            }
        }

        var inputActualizado = input with { Imagen = imagenFinal };

        if (inputActualizado.Fecha <= DateTime.Now)
        {
            ModelState.AddModelError(nameof(input.Fecha), "La fecha debe ser futura.");
        }
        if (!ModelState.IsValid)
        {
            var resumen = await _repository.ObtenerDashboardAsync(cancellationToken);
            var boletosDetalle = await _repository.ObtenerBoletosDetalleAdminAsync(cancellationToken);
            return View("Index", new AdminDashboardViewModel { Resumen = resumen, Formulario = inputActualizado, BoletosDetalle = boletosDetalle });
        }

        var resultado = await _repository.GuardarEventoAsync(inputActualizado, cancellationToken);
        if (!resultado.Exitoso)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje);
            var resumen = await _repository.ObtenerDashboardAsync(cancellationToken);
            var boletosDetalle = await _repository.ObtenerBoletosDetalleAdminAsync(cancellationToken);
            return View("Index", new AdminDashboardViewModel { Resumen = resumen, Formulario = inputActualizado, BoletosDetalle = boletosDetalle });
        }
        TempData["Mensaje"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("eventos/{id:int}/cancelar")]
    public async Task<IActionResult> CancelarEvento(int id, CancellationToken cancellationToken)
    {
        var resultado = await _repository.DesactivarEventoAsync(id, cancellationToken);
        TempData[resultado.Exitoso ? "Mensaje" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("reservas")]
    public async Task<IActionResult> Reservas(CancellationToken cancellationToken)
    {
        return View(await _repository.ObtenerReservasAdminAsync(cancellationToken));
    }

    [HttpGet("acceso")]
    public async Task<IActionResult> Acceso(CancellationToken cancellationToken)
    {
        return View(new AccesoViewModel
        {
            Eventos = await _repository.ObtenerEventosAsync(false, cancellationToken),
            Formulario = new AccesoInput()
        });
    }

    [HttpPost("acceso")]
    public async Task<IActionResult> Acceso(AccesoInput input, CancellationToken cancellationToken)
    {
        var eventos = await _repository.ObtenerEventosAsync(false, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(new AccesoViewModel { Eventos = eventos, Formulario = input });
        }
        var resultado = await _repository.ValidarAccesoAsync(input.EventoId, input.Codigo, cancellationToken);
        return View(new AccesoViewModel { Eventos = eventos, Formulario = input, Resultado = resultado });
    }

    private static EventoInput CrearFormulario(EventoResumen? evento)
    {
        if (evento is null)
        {
            return new EventoInput { Fecha = DateTime.Now.AddDays(30) };
        }
        var general = evento.Tipos.Single(x => x.Nombre == "General");
        var vip = evento.Tipos.Single(x => x.Nombre == "VIP");
        var estudiante = evento.Tipos.Single(x => x.Nombre == "Estudiante");
        return new EventoInput
        {
            Id = evento.Id,
            Titulo = evento.Titulo,
            Descripcion = evento.Descripcion,
            Fecha = DateTime.SpecifyKind(evento.Fecha, DateTimeKind.Utc).ToLocalTime(),
            Ubicacion = evento.Ubicacion,
            Imagen = evento.Imagen,
            PrecioGeneral = general.Precio,
            CapacidadGeneral = general.CapacidadTotal,
            PrecioVip = vip.Precio,
            CapacidadVip = vip.CapacidadTotal,
            PrecioEstudiante = estudiante.Precio,
            CapacidadEstudiante = estudiante.CapacidadTotal
        };
    }
}
