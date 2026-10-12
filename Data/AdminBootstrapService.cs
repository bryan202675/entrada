using Entrada.Models;
using Microsoft.AspNetCore.Identity;

namespace Entrada.Data;

public sealed class AdminBootstrapService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminBootstrapService> _logger;

    public AdminBootstrapService(IServiceProvider services, IConfiguration configuration, ILogger<AdminBootstrapService> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = _configuration["BootstrapAdmin:Email"];
        var password = _configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        try
        {
            await using var scope = _services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<TicketingRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UsuarioAutenticacion>>();
            var usuario = new UsuarioAutenticacion(0, "Administración", email, string.Empty, "Administrador");
            var hash = hasher.HashPassword(usuario, password);
            await repository.CrearAdministradorInicialAsync(usuario.Nombre, email, hash, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "No fue posible crear el administrador inicial");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
