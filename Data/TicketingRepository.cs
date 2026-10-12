using System.Data;
using Entrada.Models;
using Microsoft.Data.SqlClient;

namespace Entrada.Data;

public sealed class TicketingRepository
{
    private readonly SqlConnectionFactory _connections;

    public TicketingRepository(SqlConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<EventoResumen>> ObtenerEventosAsync(bool incluirInactivos, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT e.Id, e.Titulo, e.Descripcion, e.Fecha, e.Ubicacion, e.Imagen, e.Estado,
                   te.Id, te.Nombre, te.Precio, te.CapacidadTotal, te.nStockDisponible
            FROM dbo.Eventos e
            LEFT JOIN dbo.TiposEntrada te ON te.EventoId = e.Id
            WHERE (@IncluirInactivos = 1 OR (e.Estado = 'Activo' AND e.Fecha > SYSUTCDATETIME()))
            ORDER BY e.Fecha, e.Id, CASE te.Nombre WHEN 'General' THEN 1 WHEN 'VIP' THEN 2 ELSE 3 END;
            """;

        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IncluirInactivos", SqlDbType.Bit).Value = incluirInactivos;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await LeerEventosAsync(reader, cancellationToken);
    }

    public async Task<EventoResumen?> ObtenerEventoAsync(int id, bool incluirInactivo, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT e.Id, e.Titulo, e.Descripcion, e.Fecha, e.Ubicacion, e.Imagen, e.Estado,
                   te.Id, te.Nombre, te.Precio, te.CapacidadTotal, te.nStockDisponible
            FROM dbo.Eventos e
            LEFT JOIN dbo.TiposEntrada te ON te.EventoId = e.Id
            WHERE e.Id = @Id AND (@IncluirInactivo = 1 OR (e.Estado = 'Activo' AND e.Fecha > SYSUTCDATETIME()))
            ORDER BY CASE te.Nombre WHEN 'General' THEN 1 WHEN 'VIP' THEN 2 ELSE 3 END;
            """;

        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@IncluirInactivo", SqlDbType.Bit).Value = incluirInactivo;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return (await LeerEventosAsync(reader, cancellationToken)).SingleOrDefault();
    }

    public async Task<UsuarioAutenticacion?> ObtenerUsuarioPorEmailAsync(string email, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, Nombre, Email, PasswordHash, Rol
            FROM dbo.Usuarios
            WHERE EmailNormalizado = @Email;
            """;

        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 160).Value = NormalizarEmail(email);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new UsuarioAutenticacion(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4));
    }

    public async Task<OperacionResultado> CrearUsuarioAsync(string nombre, string email, string passwordHash, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.Usuarios (Nombre, Email, EmailNormalizado, PasswordHash, Rol)
            OUTPUT INSERTED.Id
            VALUES (@Nombre, @Email, @EmailNormalizado, @PasswordHash, 'Usuario');
            """;

        try
        {
            await using var connection = _connections.Create();
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 100).Value = nombre.Trim();
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 160).Value = email.Trim();
            command.Parameters.Add("@EmailNormalizado", SqlDbType.NVarChar, 160).Value = NormalizarEmail(email);
            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = passwordHash;
            var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            return OperacionResultado.Ok(id, "Cuenta creada");
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return OperacionResultado.Fallo("Ya existe una cuenta con ese correo.");
        }
    }

    public async Task CrearAdministradorInicialAsync(string nombre, string email, string passwordHash, CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Rol = 'Administrador')
            BEGIN
                INSERT INTO dbo.Usuarios (Nombre, Email, EmailNormalizado, PasswordHash, Rol)
                VALUES (@Nombre, @Email, @EmailNormalizado, @PasswordHash, 'Administrador');
            END
            """;
        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 100).Value = nombre.Trim();
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 160).Value = email.Trim();
        command.Parameters.Add("@EmailNormalizado", SqlDbType.NVarChar, 160).Value = NormalizarEmail(email);
        command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = passwordHash;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<OperacionResultado> CrearReservaAsync(int usuarioId, ReservaInput input, int maximo, CancellationToken cancellationToken)
    {
        if (input.Cantidad < 1 || input.Cantidad > maximo)
        {
            return OperacionResultado.Fallo($"Selecciona entre 1 y {maximo} entradas.");
        }

        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            const string descontarSql = """
                UPDATE te WITH (UPDLOCK, ROWLOCK)
                SET te.nStockDisponible = te.nStockDisponible - @Cantidad
                FROM dbo.TiposEntrada te
                INNER JOIN dbo.Eventos e ON e.Id = te.EventoId
                WHERE te.Id = @TipoId
                  AND te.EventoId = @EventoId
                  AND e.Estado = 'Activo'
                  AND e.Fecha > SYSUTCDATETIME()
                  AND te.nStockDisponible >= @Cantidad;
                """;

            await using (var descontar = new SqlCommand(descontarSql, connection, (SqlTransaction)transaction))
            {
                descontar.Parameters.Add("@Cantidad", SqlDbType.Int).Value = input.Cantidad;
                descontar.Parameters.Add("@TipoId", SqlDbType.Int).Value = input.TipoEntradaId;
                descontar.Parameters.Add("@EventoId", SqlDbType.Int).Value = input.EventoId;
                if (await descontar.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return OperacionResultado.Fallo("La categoría ya no está disponible en la cantidad solicitada.");
                }
            }

            const string precioSql = "SELECT Precio FROM dbo.TiposEntrada WHERE Id = @TipoId AND EventoId = @EventoId;";
            decimal precio;
            await using (var precioCommand = new SqlCommand(precioSql, connection, (SqlTransaction)transaction))
            {
                precioCommand.Parameters.Add("@TipoId", SqlDbType.Int).Value = input.TipoEntradaId;
                precioCommand.Parameters.Add("@EventoId", SqlDbType.Int).Value = input.EventoId;
                precio = Convert.ToDecimal(await precioCommand.ExecuteScalarAsync(cancellationToken));
            }

            const string reservaSql = """
                INSERT INTO dbo.Reservas (UsuarioId, EventoId, Total, Estado)
                OUTPUT INSERTED.Id
                VALUES (@UsuarioId, @EventoId, @Total, 'Confirmada');
                """;
            long reservaId;
            await using (var reservaCommand = new SqlCommand(reservaSql, connection, (SqlTransaction)transaction))
            {
                reservaCommand.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
                reservaCommand.Parameters.Add("@EventoId", SqlDbType.Int).Value = input.EventoId;
                reservaCommand.Parameters.Add("@Total", SqlDbType.Decimal).Value = precio * input.Cantidad;
                reservaCommand.Parameters["@Total"].Precision = 12;
                reservaCommand.Parameters["@Total"].Scale = 2;
                reservaId = Convert.ToInt64(await reservaCommand.ExecuteScalarAsync(cancellationToken));
            }

            const string secuenciaSql = "SELECT NEXT VALUE FOR dbo.SeqCodigoBoleto;";
            const string boletoSql = """
                INSERT INTO dbo.Boletos (ReservaId, TipoEntradaId, CodigoUnico, Estado)
                VALUES (@ReservaId, @TipoId, @Codigo, 'Activo');
                """;

            for (var index = 0; index < input.Cantidad; index++)
            {
                long correlativo;
                await using (var secuencia = new SqlCommand(secuenciaSql, connection, (SqlTransaction)transaction))
                {
                    correlativo = Convert.ToInt64(await secuencia.ExecuteScalarAsync(cancellationToken));
                }

                await using var boleto = new SqlCommand(boletoSql, connection, (SqlTransaction)transaction);
                boleto.Parameters.Add("@ReservaId", SqlDbType.BigInt).Value = reservaId;
                boleto.Parameters.Add("@TipoId", SqlDbType.Int).Value = input.TipoEntradaId;
                boleto.Parameters.Add("@Codigo", SqlDbType.VarChar, 30).Value = $"ENT-{correlativo:D6}";
                await boleto.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return OperacionResultado.Ok(reservaId, "Reserva confirmada");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ReservaResumen?> ObtenerReservaAsync(long reservaId, int usuarioId, bool administrador, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.Id, r.FechaReserva, r.Total, r.Estado, e.Id, e.Titulo, e.Fecha, e.Ubicacion,
                   b.Id, b.CodigoUnico, te.Nombre, b.Estado, b.FechaUso
            FROM dbo.Reservas r
            INNER JOIN dbo.Eventos e ON e.Id = r.EventoId
            INNER JOIN dbo.Boletos b ON b.ReservaId = r.Id
            INNER JOIN dbo.TiposEntrada te ON te.Id = b.TipoEntradaId
            WHERE r.Id = @ReservaId AND (@Administrador = 1 OR r.UsuarioId = @UsuarioId)
            ORDER BY b.Id;
            """;

        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ReservaId", SqlDbType.BigInt).Value = reservaId;
        command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
        command.Parameters.Add("@Administrador", SqlDbType.Bit).Value = administrador;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await LeerReservaAsync(reader, cancellationToken);
    }

    public async Task<IReadOnlyList<ReservaResumen>> ObtenerReservasUsuarioAsync(int usuarioId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.Id, r.FechaReserva, r.Total, r.Estado, e.Id, e.Titulo, e.Fecha, e.Ubicacion,
                   b.Id, b.CodigoUnico, te.Nombre, b.Estado, b.FechaUso
            FROM dbo.Reservas r
            INNER JOIN dbo.Eventos e ON e.Id = r.EventoId
            INNER JOIN dbo.Boletos b ON b.ReservaId = r.Id
            INNER JOIN dbo.TiposEntrada te ON te.Id = b.TipoEntradaId
            WHERE r.UsuarioId = @UsuarioId
            ORDER BY r.FechaReserva DESC, b.Id;
            """;

        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await LeerReservasAsync(reader, cancellationToken);
    }

    public async Task<DashboardResumen> ObtenerDashboardAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM dbo.Eventos),
                (SELECT COUNT(*) FROM dbo.Reservas WHERE Estado = 'Confirmada'),
                (SELECT COUNT(*) FROM dbo.Boletos),
                (SELECT COUNT(*) FROM dbo.Boletos WHERE Estado = 'Utilizado'),
                COALESCE((SELECT SUM(CapacidadTotal) FROM dbo.TiposEntrada), 0),
                COALESCE((SELECT SUM(nStockDisponible) FROM dbo.TiposEntrada te INNER JOIN dbo.Eventos e ON e.Id = te.EventoId WHERE e.Estado = 'Activo'), 0);
            """;

        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        int eventos;
        int reservas;
        int boletos;
        int utilizados;
        int capacidad;
        int disponibles;
        await using (var command = new SqlCommand(sql, connection))
        await using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            eventos = reader.GetInt32(0);
            reservas = reader.GetInt32(1);
            boletos = reader.GetInt32(2);
            utilizados = reader.GetInt32(3);
            capacidad = reader.GetInt32(4);
            disponibles = reader.GetInt32(5);
        }

        var lista = await ObtenerEventosAsync(true, cancellationToken);
        return new DashboardResumen(eventos, reservas, boletos, utilizados, capacidad, disponibles, lista);
    }

    public async Task<OperacionResultado> GuardarEventoAsync(EventoInput input, CancellationToken cancellationToken)
    {
        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            int eventoId;
            if (input.Id.HasValue)
            {
                const string actualizarSql = """
                    UPDATE dbo.Eventos
                    SET Titulo = @Titulo, Descripcion = @Descripcion, Fecha = @Fecha,
                        Ubicacion = @Ubicacion, Imagen = @Imagen
                    WHERE Id = @Id;
                    """;
                await using var actualizar = CrearComandoEvento(actualizarSql, input, connection, (SqlTransaction)transaction);
                actualizar.Parameters.Add("@Id", SqlDbType.Int).Value = input.Id.Value;
                if (await actualizar.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return OperacionResultado.Fallo("El evento ya no existe.");
                }
                eventoId = input.Id.Value;
            }
            else
            {
                const string insertarSql = """
                    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
                    OUTPUT INSERTED.Id
                    VALUES (@Titulo, @Descripcion, @Fecha, @Ubicacion, @Imagen, 'Activo');
                    """;
                await using var insertar = CrearComandoEvento(insertarSql, input, connection, (SqlTransaction)transaction);
                eventoId = Convert.ToInt32(await insertar.ExecuteScalarAsync(cancellationToken));
            }

            var categorias = new[]
            {
                (Nombre: "General", Precio: input.PrecioGeneral, Capacidad: input.CapacidadGeneral),
                (Nombre: "VIP", Precio: input.PrecioVip, Capacidad: input.CapacidadVip),
                (Nombre: "Estudiante", Precio: input.PrecioEstudiante, Capacidad: input.CapacidadEstudiante)
            };

            foreach (var categoria in categorias)
            {
                OperacionResultado resultado = input.Id.HasValue
                    ? await ActualizarCategoriaAsync(connection, (SqlTransaction)transaction, eventoId, categoria.Nombre, categoria.Precio, categoria.Capacidad, cancellationToken)
                    : await InsertarCategoriaAsync(connection, (SqlTransaction)transaction, eventoId, categoria.Nombre, categoria.Precio, categoria.Capacidad, cancellationToken);

                if (!resultado.Exitoso)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return resultado;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return OperacionResultado.Ok(eventoId, input.Id.HasValue ? "Evento actualizado" : "Evento creado");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<OperacionResultado> DesactivarEventoAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.Eventos SET Estado = 'Cancelado' WHERE Id = @Id AND Estado = 'Activo';";
        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? OperacionResultado.Ok(id, "Evento cancelado")
            : OperacionResultado.Fallo("El evento ya estaba cancelado o no existe.");
    }

    public async Task<IReadOnlyList<ReservaAdmin>> ObtenerReservasAdminAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.Id, u.Nombre, u.Email, e.Titulo, r.FechaReserva,
                   COUNT(b.Id), r.Total, r.Estado
            FROM dbo.Reservas r
            INNER JOIN dbo.Usuarios u ON u.Id = r.UsuarioId
            INNER JOIN dbo.Eventos e ON e.Id = r.EventoId
            INNER JOIN dbo.Boletos b ON b.ReservaId = r.Id
            GROUP BY r.Id, u.Nombre, u.Email, e.Titulo, r.FechaReserva, r.Total, r.Estado
            ORDER BY r.FechaReserva DESC;
            """;

        var resultados = new List<ReservaAdmin>();
        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt64(0);
            resultados.Add(new ReservaAdmin(id, $"RES-{id:D6}", reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDateTime(4), reader.GetInt32(5), reader.GetDecimal(6), reader.GetString(7)));
        }
        return resultados;
    }

    public async Task<AccesoResultado> ValidarAccesoAsync(int eventoId, string codigo, CancellationToken cancellationToken)
    {
        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            const string usarSql = """
                UPDATE b WITH (UPDLOCK, ROWLOCK)
                SET b.Estado = 'Utilizado', b.FechaUso = SYSUTCDATETIME()
                OUTPUT e.Titulo, u.Nombre, te.Nombre, INSERTED.FechaUso
                FROM dbo.Boletos b
                INNER JOIN dbo.Reservas r ON r.Id = b.ReservaId
                INNER JOIN dbo.Eventos e ON e.Id = r.EventoId
                INNER JOIN dbo.Usuarios u ON u.Id = r.UsuarioId
                INNER JOIN dbo.TiposEntrada te ON te.Id = b.TipoEntradaId
                WHERE b.CodigoUnico = @Codigo AND e.Id = @EventoId
                  AND b.Estado = 'Activo' AND r.Estado = 'Confirmada' AND e.Estado = 'Activo';
                """;

            await using (var usar = new SqlCommand(usarSql, connection, (SqlTransaction)transaction))
            {
                usar.Parameters.Add("@Codigo", SqlDbType.VarChar, 30).Value = codigo.Trim().ToUpperInvariant();
                usar.Parameters.Add("@EventoId", SqlDbType.Int).Value = eventoId;
                await using var reader = await usar.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var resultado = new AccesoResultado(true, true, "Acceso autorizado", reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3));
                    await reader.DisposeAsync();
                    await transaction.CommitAsync(cancellationToken);
                    return resultado;
                }
            }

            const string estadoSql = """
                SELECT e.Id, e.Titulo, u.Nombre, te.Nombre, b.Estado, b.FechaUso, r.Estado, e.Estado
                FROM dbo.Boletos b
                INNER JOIN dbo.Reservas r ON r.Id = b.ReservaId
                INNER JOIN dbo.Eventos e ON e.Id = r.EventoId
                INNER JOIN dbo.Usuarios u ON u.Id = r.UsuarioId
                INNER JOIN dbo.TiposEntrada te ON te.Id = b.TipoEntradaId
                WHERE b.CodigoUnico = @Codigo;
                """;
            await using var estado = new SqlCommand(estadoSql, connection, (SqlTransaction)transaction);
            estado.Parameters.Add("@Codigo", SqlDbType.VarChar, 30).Value = codigo.Trim().ToUpperInvariant();
            await using var estadoReader = await estado.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            AccesoResultado fallo;
            if (!await estadoReader.ReadAsync(cancellationToken))
            {
                fallo = new AccesoResultado(false, false, "El código no corresponde a un boleto emitido.", null, null, null, null);
            }
            else
            {
                var eventoCorrecto = estadoReader.GetInt32(0) == eventoId;
                var usado = estadoReader.GetString(4) == "Utilizado";
                var mensaje = !eventoCorrecto
                    ? "El boleto pertenece a otro evento."
                    : usado
                        ? "Este boleto ya fue utilizado y no permite un segundo ingreso."
                        : "El boleto o su evento están cancelados.";
                fallo = new AccesoResultado(true, false, mensaje, estadoReader.GetString(1), estadoReader.GetString(2), estadoReader.GetString(3), estadoReader.IsDBNull(5) ? null : estadoReader.GetDateTime(5));
            }
            await estadoReader.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
            return fallo;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<IReadOnlyList<EventoResumen>> LeerEventosAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        var eventos = new Dictionary<int, EventoAcumulado>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(0);
            if (!eventos.TryGetValue(id, out var actual))
            {
                actual = new EventoAcumulado(id, reader.GetString(1), reader.GetString(2), reader.GetDateTime(3), reader.GetString(4), reader.GetString(5), reader.GetString(6));
                eventos.Add(id, actual);
            }
            if (!reader.IsDBNull(7))
            {
                actual.Tipos.Add(new TipoEntradaResumen(reader.GetInt32(7), reader.GetString(8), reader.GetDecimal(9), reader.GetInt32(10), reader.GetInt32(11)));
            }
        }

        return eventos.Values.Select(x => x.Crear()).ToList();
    }

    private static async Task<ReservaResumen?> LeerReservaAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        var reservas = await LeerReservasAsync(reader, cancellationToken);
        return reservas.SingleOrDefault();
    }

    private static async Task<IReadOnlyList<ReservaResumen>> LeerReservasAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        var reservas = new Dictionary<long, ReservaAcumulada>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt64(0);
            if (!reservas.TryGetValue(id, out var actual))
            {
                actual = new ReservaAcumulada(id, reader.GetDateTime(1), reader.GetDecimal(2), reader.GetString(3), reader.GetInt32(4), reader.GetString(5), reader.GetDateTime(6), reader.GetString(7));
                reservas.Add(id, actual);
            }
            actual.Boletos.Add(new BoletoResumen(reader.GetInt64(8), reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetDateTime(12)));
        }
        return reservas.Values.Select(x => x.Crear()).ToList();
    }

    private static SqlCommand CrearComandoEvento(string sql, EventoInput input, SqlConnection connection, SqlTransaction transaction)
    {
        var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Titulo", SqlDbType.NVarChar, 140).Value = input.Titulo.Trim();
        command.Parameters.Add("@Descripcion", SqlDbType.NVarChar, 1200).Value = input.Descripcion.Trim();
        command.Parameters.Add("@Fecha", SqlDbType.DateTime2).Value = input.Fecha.ToUniversalTime();
        command.Parameters.Add("@Ubicacion", SqlDbType.NVarChar, 180).Value = input.Ubicacion.Trim();
        command.Parameters.Add("@Imagen", SqlDbType.NVarChar, 300).Value = input.Imagen.Trim();
        return command;
    }

    private static async Task<OperacionResultado> InsertarCategoriaAsync(SqlConnection connection, SqlTransaction transaction, int eventoId, string nombre, decimal precio, int capacidad, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
            VALUES (@EventoId, @Nombre, @Precio, @Capacidad, @Capacidad);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        AgregarParametrosCategoria(command, eventoId, nombre, precio, capacidad);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return OperacionResultado.Ok();
    }

    private static async Task<OperacionResultado> ActualizarCategoriaAsync(SqlConnection connection, SqlTransaction transaction, int eventoId, string nombre, decimal precio, int capacidad, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.TiposEntrada
            SET Precio = @Precio,
                nStockDisponible = @Capacidad - (CapacidadTotal - nStockDisponible),
                CapacidadTotal = @Capacidad
            WHERE EventoId = @EventoId AND Nombre = @Nombre
              AND @Capacidad >= (CapacidadTotal - nStockDisponible);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        AgregarParametrosCategoria(command, eventoId, nombre, precio, capacidad);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? OperacionResultado.Ok()
            : OperacionResultado.Fallo($"La capacidad de {nombre} no puede ser menor que los boletos ya emitidos.");
    }

    private static void AgregarParametrosCategoria(SqlCommand command, int eventoId, string nombre, decimal precio, int capacidad)
    {
        command.Parameters.Add("@EventoId", SqlDbType.Int).Value = eventoId;
        command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 30).Value = nombre;
        command.Parameters.Add("@Precio", SqlDbType.Decimal).Value = precio;
        command.Parameters["@Precio"].Precision = 10;
        command.Parameters["@Precio"].Scale = 2;
        command.Parameters.Add("@Capacidad", SqlDbType.Int).Value = capacidad;
    }

    private static string NormalizarEmail(string email) => email.Trim().ToUpperInvariant();

    private sealed class EventoAcumulado
    {
        public EventoAcumulado(int id, string titulo, string descripcion, DateTime fecha, string ubicacion, string imagen, string estado)
        {
            Id = id;
            Titulo = titulo;
            Descripcion = descripcion;
            Fecha = fecha;
            Ubicacion = ubicacion;
            Imagen = imagen;
            Estado = estado;
        }

        public int Id { get; }
        public string Titulo { get; }
        public string Descripcion { get; }
        public DateTime Fecha { get; }
        public string Ubicacion { get; }
        public string Imagen { get; }
        public string Estado { get; }
        public List<TipoEntradaResumen> Tipos { get; } = [];

        public EventoResumen Crear() => new(Id, Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado, Tipos.Sum(x => x.CapacidadTotal), Tipos.Sum(x => x.StockDisponible), Tipos.Count == 0 ? 0 : Tipos.Min(x => x.Precio), Tipos);
    }

    private sealed class ReservaAcumulada
    {
        public ReservaAcumulada(long id, DateTime fechaReserva, decimal total, string estado, int eventoId, string evento, DateTime fechaEvento, string ubicacion)
        {
            Id = id;
            FechaReserva = fechaReserva;
            Total = total;
            Estado = estado;
            EventoId = eventoId;
            Evento = evento;
            FechaEvento = fechaEvento;
            Ubicacion = ubicacion;
        }

        public long Id { get; }
        public DateTime FechaReserva { get; }
        public decimal Total { get; }
        public string Estado { get; }
        public int EventoId { get; }
        public string Evento { get; }
        public DateTime FechaEvento { get; }
        public string Ubicacion { get; }
        public List<BoletoResumen> Boletos { get; } = [];

        public ReservaResumen Crear() => new(Id, $"RES-{Id:D6}", FechaReserva, Total, Estado, Boletos.Count, Boletos.FirstOrDefault()?.Tipo ?? string.Empty, EventoId, Evento, FechaEvento, Ubicacion, Boletos);
    }
}
