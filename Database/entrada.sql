IF DB_ID(N'EntradaDb') IS NULL
BEGIN
    CREATE DATABASE EntradaDb;
END
GO

USE EntradaDb;
GO

IF OBJECT_ID(N'dbo.SeqCodigoBoleto', N'SO') IS NULL
BEGIN
    CREATE SEQUENCE dbo.SeqCodigoBoleto AS BIGINT START WITH 1 INCREMENT BY 1;
END
GO

IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
        Nombre NVARCHAR(100) NOT NULL,
        Email NVARCHAR(160) NOT NULL,
        EmailNormalizado NVARCHAR(160) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        Rol NVARCHAR(20) NOT NULL CONSTRAINT DF_Usuarios_Rol DEFAULT N'Usuario',
        FechaRegistro DATETIME2(0) NOT NULL CONSTRAINT DF_Usuarios_FechaRegistro DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_Usuarios_Rol CHECK (Rol IN (N'Administrador', N'Usuario'))
    );
    CREATE UNIQUE INDEX UX_Usuarios_EmailNormalizado ON dbo.Usuarios(EmailNormalizado);
END
GO

IF OBJECT_ID(N'dbo.Eventos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Eventos
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eventos PRIMARY KEY,
        Titulo NVARCHAR(140) NOT NULL,
        Descripcion NVARCHAR(1200) NOT NULL,
        Fecha DATETIME2(0) NOT NULL,
        Ubicacion NVARCHAR(180) NOT NULL,
        Imagen NVARCHAR(300) NOT NULL,
        Estado NVARCHAR(20) NOT NULL CONSTRAINT DF_Eventos_Estado DEFAULT N'Activo',
        FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_Eventos_FechaCreacion DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_Eventos_Estado CHECK (Estado IN (N'Activo', N'Cancelado'))
    );
    CREATE INDEX IX_Eventos_Estado_Fecha ON dbo.Eventos(Estado, Fecha);
END
GO

IF OBJECT_ID(N'dbo.TiposEntrada', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TiposEntrada
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TiposEntrada PRIMARY KEY,
        EventoId INT NOT NULL,
        Nombre NVARCHAR(30) NOT NULL,
        Precio DECIMAL(10,2) NOT NULL,
        CapacidadTotal INT NOT NULL,
        nStockDisponible INT NOT NULL,
        CONSTRAINT FK_TiposEntrada_Eventos FOREIGN KEY (EventoId) REFERENCES dbo.Eventos(Id),
        CONSTRAINT UQ_TiposEntrada_Evento_Nombre UNIQUE (EventoId, Nombre),
        CONSTRAINT CK_TiposEntrada_Nombre CHECK (Nombre IN (N'General', N'VIP', N'Estudiante')),
        CONSTRAINT CK_TiposEntrada_Precio CHECK (Precio >= 0),
        CONSTRAINT CK_TiposEntrada_Capacidad CHECK (CapacidadTotal > 0),
        CONSTRAINT CK_TiposEntrada_Stock CHECK (nStockDisponible >= 0 AND nStockDisponible <= CapacidadTotal)
    );
END
GO

IF OBJECT_ID(N'dbo.Reservas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Reservas
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Reservas PRIMARY KEY,
        UsuarioId INT NOT NULL,
        EventoId INT NOT NULL,
        FechaReserva DATETIME2(0) NOT NULL CONSTRAINT DF_Reservas_FechaReserva DEFAULT SYSUTCDATETIME(),
        Total DECIMAL(12,2) NOT NULL,
        Estado NVARCHAR(20) NOT NULL CONSTRAINT DF_Reservas_Estado DEFAULT N'Confirmada',
        CONSTRAINT FK_Reservas_Usuarios FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios(Id),
        CONSTRAINT FK_Reservas_Eventos FOREIGN KEY (EventoId) REFERENCES dbo.Eventos(Id),
        CONSTRAINT CK_Reservas_Total CHECK (Total >= 0),
        CONSTRAINT CK_Reservas_Estado CHECK (Estado IN (N'Confirmada', N'Cancelada'))
    );
    CREATE INDEX IX_Reservas_Usuario_Fecha ON dbo.Reservas(UsuarioId, FechaReserva DESC);
    CREATE INDEX IX_Reservas_Evento ON dbo.Reservas(EventoId);
END
GO

IF OBJECT_ID(N'dbo.Boletos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Boletos
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Boletos PRIMARY KEY,
        ReservaId BIGINT NOT NULL,
        TipoEntradaId INT NOT NULL,
        CodigoUnico VARCHAR(30) NOT NULL,
        Estado NVARCHAR(20) NOT NULL CONSTRAINT DF_Boletos_Estado DEFAULT N'Activo',
        FechaUso DATETIME2(0) NULL,
        CONSTRAINT FK_Boletos_Reservas FOREIGN KEY (ReservaId) REFERENCES dbo.Reservas(Id),
        CONSTRAINT FK_Boletos_TiposEntrada FOREIGN KEY (TipoEntradaId) REFERENCES dbo.TiposEntrada(Id),
        CONSTRAINT UQ_Boletos_CodigoUnico UNIQUE (CodigoUnico),
        CONSTRAINT CK_Boletos_Estado CHECK (Estado IN (N'Activo', N'Utilizado', N'Cancelado')),
        CONSTRAINT CK_Boletos_FechaUso CHECK ((Estado = N'Utilizado' AND FechaUso IS NOT NULL) OR (Estado <> N'Utilizado' AND FechaUso IS NULL))
    );
    CREATE INDEX IX_Boletos_Reserva ON dbo.Boletos(ReservaId);
    CREATE INDEX IX_Boletos_TipoEntrada ON dbo.Boletos(TipoEntradaId);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos)
BEGIN
    DECLARE @TeatroId INT;
    DECLARE @FestivalId INT;
    DECLARE @ConciertoId INT;

    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'La casa de Bernarda Alba', N'Una puesta en escena estudiantil de gran formato, con dirección contemporánea y elenco del taller de artes escénicas.', DATEADD(DAY, 35, SYSUTCDATETIME()), N'Teatro Nacional · Sala Principal', N'/images/evento-teatro.jpg', N'Activo');
    SET @TeatroId = SCOPE_IDENTITY();

    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Festival Trazo Joven', N'Ilustración, fotografía, instalación y diseño conviven en una jornada dedicada a las nuevas voces creativas.', DATEADD(DAY, 52, SYSUTCDATETIME()), N'Centro Cultural · Patio Norte', N'/images/evento-arte.jpg', N'Activo');
    SET @FestivalId = SCOPE_IDENTITY();

    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Concierto Noche Eléctrica', N'Bandas escolares presentan repertorios originales en un encuentro de música en vivo abierto a toda la comunidad.', DATEADD(DAY, 68, SYSUTCDATETIME()), N'Auditorio Central', N'/images/evento-musica.jpg', N'Activo');
    SET @ConciertoId = SCOPE_IDENTITY();

    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES
        (@TeatroId, N'General', 12.00, 180, 180),
        (@TeatroId, N'VIP', 25.00, 40, 40),
        (@TeatroId, N'Estudiante', 7.00, 120, 120),
        (@FestivalId, N'General', 8.00, 250, 250),
        (@FestivalId, N'VIP', 18.00, 50, 50),
        (@FestivalId, N'Estudiante', 4.00, 180, 180),
        (@ConciertoId, N'General', 15.00, 300, 300),
        (@ConciertoId, N'VIP', 32.00, 60, 60),
        (@ConciertoId, N'Estudiante', 9.00, 200, 200);
END
GO

UPDATE dbo.Eventos
SET Imagen = CASE Imagen
    WHEN N'/images/evento-teatro.svg' THEN N'/images/evento-teatro.jpg'
    WHEN N'/images/evento-arte.svg' THEN N'/images/evento-arte.jpg'
    WHEN N'/images/evento-musica.svg' THEN N'/images/evento-musica.jpg'
    ELSE Imagen
END
WHERE Imagen IN (N'/images/evento-teatro.svg', N'/images/evento-arte.svg', N'/images/evento-musica.svg');
GO
