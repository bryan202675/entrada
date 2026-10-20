using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Entrada.Models;

public sealed record TipoEntradaResumen(int Id, string Nombre, decimal Precio, int CapacidadTotal, int StockDisponible);

public sealed record EventoResumen(
    int Id,
    string Titulo,
    string Descripcion,
    DateTime Fecha,
    string Ubicacion,
    string Imagen,
    string Estado,
    int CapacidadTotal,
    int StockDisponible,
    decimal PrecioDesde,
    IReadOnlyList<TipoEntradaResumen> Tipos);

public sealed record BoletoResumen(long Id, string CodigoUnico, string Tipo, string Estado, DateTime? FechaUso);

public sealed record ReservaResumen(
    long Id,
    string Identificador,
    DateTime FechaReserva,
    decimal Total,
    string Estado,
    int Cantidad,
    string Tipo,
    int EventoId,
    string Evento,
    DateTime FechaEvento,
    string Ubicacion,
    IReadOnlyList<BoletoResumen> Boletos);

public sealed record UsuarioAutenticacion(int Id, string Nombre, string Email, string PasswordHash, string Rol);

public sealed record DashboardResumen(
    int Eventos,
    int Reservas,
    int Boletos,
    int Utilizados,
    int CapacidadTotal,
    int Disponibles,
    IReadOnlyList<EventoResumen> EventosActivos);

public sealed record ReservaAdmin(
    long Id,
    string Identificador,
    string Usuario,
    string Email,
    string Evento,
    DateTime FechaReserva,
    int Cantidad,
    decimal Total,
    string Estado);

public sealed record BoletoDetalleAdmin(
    long BoletoId,
    string CodigoBoleto,
    string TipoEntrada,
    decimal Precio,
    string EstadoBoleto,
    DateTime? FechaUso,
    long ReservaId,
    string IdentificadorReserva,
    string UsuarioNombre,
    string UsuarioEmail,
    int EventoId,
    string EventoTitulo,
    DateTime FechaEvento,
    DateTime FechaReserva,
    string EstadoReserva);

public sealed record AccesoResultado(bool Encontrado, bool Permitido, string Mensaje, string? Evento, string? Asistente, string? Tipo, DateTime? FechaUso);

public sealed record OperacionResultado(bool Exitoso, string Mensaje, long? Id = null)
{
    public static OperacionResultado Ok(long? id = null, string mensaje = "Operación completada") => new(true, mensaje, id);
    public static OperacionResultado Fallo(string mensaje) => new(false, mensaje);
}

public sealed record CatalogoViewModel(IReadOnlyList<EventoResumen> Eventos, string? Error);

public sealed class RegistroInput
{
    [Required(ErrorMessage = "Escribe tu nombre.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "Escribe tu correo.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [StringLength(160)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Crea una contraseña.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Usa al menos 8 caracteres.")]
    [DataType(DataType.Password)]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginInput
{
    [Required(ErrorMessage = "Escribe tu correo.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Escribe tu contraseña.")]
    [DataType(DataType.Password)]
    public string Password { get; init; } = string.Empty;

    public string? ReturnUrl { get; init; }
}

public sealed class ReservaInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un evento válido.")]
    public int EventoId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un tipo de entrada.")]
    public int TipoEntradaId { get; init; }

    [Range(1, 20, ErrorMessage = "Selecciona una cantidad válida.")]
    public int Cantidad { get; init; } = 1;
}

public sealed class ReservaViewModel
{
    public required EventoResumen Evento { get; init; }
    public required ReservaInput Formulario { get; init; }
    public int MaximoPorReserva { get; init; }
}

public sealed record EventoInput
{
    public int? Id { get; init; }

    [Required(ErrorMessage = "Escribe el nombre del evento.")]
    [StringLength(140)]
    public string Titulo { get; init; } = string.Empty;

    [Required(ErrorMessage = "Describe el evento.")]
    [StringLength(1200, MinimumLength = 20, ErrorMessage = "La descripción debe tener entre 20 y 1200 caracteres.")]
    public string Descripcion { get; init; } = string.Empty;

    [Required(ErrorMessage = "Selecciona fecha y hora.")]
    public DateTime Fecha { get; init; }

    [Required(ErrorMessage = "Escribe la ubicación.")]
    [StringLength(180)]
    public string Ubicacion { get; init; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string Imagen { get; init; } = "/images/evento-arte.jpg";

    public IFormFile? ArchivoImagen { get; init; }

    [Range(0, 10000)]
    public decimal PrecioGeneral { get; init; }

    [Range(1, 100000)]
    public int CapacidadGeneral { get; init; } = 100;

    [Range(0, 10000)]
    public decimal PrecioVip { get; init; }

    [Range(1, 100000)]
    public int CapacidadVip { get; init; } = 30;

    [Range(0, 10000)]
    public decimal PrecioEstudiante { get; init; }

    [Range(1, 100000)]
    public int CapacidadEstudiante { get; init; } = 80;
}

public sealed class AdminDashboardViewModel
{
    public required DashboardResumen Resumen { get; init; }
    public required EventoInput Formulario { get; init; }
    public IReadOnlyList<BoletoDetalleAdmin> BoletosDetalle { get; init; } = Array.Empty<BoletoDetalleAdmin>();
    public bool Editando => Formulario.Id.HasValue;
}

public sealed class AccesoInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el evento.")]
    public int EventoId { get; init; }

    [Required(ErrorMessage = "Escribe el código del boleto o de la reserva.")]
    [RegularExpression(@"^\s*(?:([Ee][Nn][Tt]|[Rr][Ee][Ss])-?)?[0-9]{1,12}\s*$", ErrorMessage = "Usa un código válido con formato ENT-000001 o RES-000001.")]
    public string Codigo { get; init; } = string.Empty;
}

public sealed class AccesoViewModel
{
    public required IReadOnlyList<EventoResumen> Eventos { get; init; }
    public required AccesoInput Formulario { get; init; }
    public AccesoResultado? Resultado { get; init; }
}
