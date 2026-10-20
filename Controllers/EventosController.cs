using System.Security.Claims;
using Entrada.Data;
using Entrada.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Entrada.Controllers;

[Route("eventos")]
public sealed class EventosController : Controller
{
    private readonly TicketingRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EventosController> _logger;

    public EventosController(TicketingRepository repository, IConfiguration configuration, ILogger<EventosController> logger)
    {
        _repository = repository;
        _configuration = configuration;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var eventos = await _repository.ObtenerEventosAsync(false, cancellationToken);
            return View(new CatalogoViewModel(eventos, null));
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "No fue posible cargar la programación");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(new CatalogoViewModel([], "No pudimos cargar la programación. Comprueba que SQL Server esté disponible e inténtalo de nuevo."));
        }
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/reservar")]
    public async Task<IActionResult> Reserva(int id, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Administrador"))
        {
            return RedirectToAction("Index", "Admin", new { editar = id });
        }
        var evento = await _repository.ObtenerEventoAsync(id, false, cancellationToken);
        if (evento is null)
        {
            return NotFound();
        }
        return View(CrearViewModel(evento, new ReservaInput { EventoId = id, TipoEntradaId = evento.Tipos.FirstOrDefault()?.Id ?? 0 }));
    }

    [Authorize]
    [HttpPost("reservar")]
    public async Task<IActionResult> ConfirmarReserva(ReservaInput input, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Administrador"))
        {
            return RedirectToAction("Index", "Admin");
        }
        var maximo = MaximoPorReserva();
        if (input.Cantidad > maximo)
        {
            ModelState.AddModelError(nameof(input.Cantidad), $"Puedes reservar un máximo de {maximo} entradas.");
        }

        var evento = await _repository.ObtenerEventoAsync(input.EventoId, false, cancellationToken);
        if (evento is null)
        {
            ModelState.AddModelError(string.Empty, "El evento no existe, fue cancelado o ya ocurrió.");
            return View("Reserva", CrearViewModelVacio(input));
        }

        if (!ModelState.IsValid)
        {
            return View("Reserva", CrearViewModel(evento, input));
        }

        try
        {
            var resultado = await _repository.CrearReservaAsync(UsuarioId(), input, maximo, cancellationToken);
            if (!resultado.Exitoso)
            {
                ModelState.AddModelError(string.Empty, resultado.Mensaje);
                return View("Reserva", CrearViewModel(evento, input));
            }
            return RedirectToAction(nameof(Confirmacion), new { id = resultado.Id });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falló la reserva del evento {EventoId}", input.EventoId);
            ModelState.AddModelError(string.Empty, "No pudimos completar la reserva. No se realizó ningún cargo ni descuento de cupo.");
            return View("Reserva", CrearViewModel(evento, input));
        }
    }

    [Authorize]
    [HttpGet("reservas/{id:long}/confirmacion")]
    public async Task<IActionResult> Confirmacion(long id, CancellationToken cancellationToken)
    {
        var reserva = await _repository.ObtenerReservaAsync(id, UsuarioId(), User.IsInRole("Administrador"), cancellationToken);
        return reserva is null ? NotFound() : View(reserva);
    }

    private ReservaViewModel CrearViewModel(EventoResumen evento, ReservaInput input) => new()
    {
        Evento = evento,
        Formulario = input,
        MaximoPorReserva = MaximoPorReserva()
    };

    private ReservaViewModel CrearViewModelVacio(ReservaInput input) => CrearViewModel(
        new EventoResumen(input.EventoId, "Evento no disponible", string.Empty, DateTime.UtcNow, string.Empty, string.Empty, "No disponible", 0, 0, 0, []), input);

    private int MaximoPorReserva() => Math.Clamp(_configuration.GetValue("Ticketing:MaximoPorReserva", 8), 1, 20);

    private int UsuarioId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
