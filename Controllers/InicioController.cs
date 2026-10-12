using Entrada.Data;
using Entrada.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Entrada.Controllers;

public sealed class InicioController : Controller
{
    private readonly TicketingRepository _repository;
    private readonly ILogger<InicioController> _logger;

    public InicioController(TicketingRepository repository, ILogger<InicioController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var eventos = await _repository.ObtenerEventosAsync(false, cancellationToken);
            return View(new CatalogoViewModel(eventos, null));
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "No fue posible cargar el catálogo");
            var mensaje = exception.Number is 53 or -2 or 10060 or 10061
                ? "El servicio de eventos no está disponible en este momento. Verifica que SQL Server esté iniciado e inténtalo de nuevo."
                : "No pudimos consultar la cartelera. Inténtalo nuevamente en unos instantes.";
            return View(new CatalogoViewModel([], mensaje));
        }
    }

    [HttpGet("inicio/error")]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature is not null)
        {
            _logger.LogError(feature.Error, "Error no controlado en {Path}", feature.Path);
        }
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View();
    }
}
