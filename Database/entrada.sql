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

-- Eventos de Octubre, Noviembre y Diciembre
DECLARE @EvId INT;

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos WHERE Titulo = N'Gala de Danza Contemporánea: Raíces en Vuelo')
BEGIN
    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Gala de Danza Contemporánea: Raíces en Vuelo', N'Una experiencia coreográfica que fusiona tradiciones ancestrales con el lenguaje de la danza contemporánea latinoamericana.', '2026-10-24 02:00:00', N'Teatro Nacional · Gran Sala', N'/images/evento-teatro.jpg', N'Activo');
    SET @EvId = SCOPE_IDENTITY();
    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES (@EvId, N'General', 10.00, 180, 180), (@EvId, N'VIP', 22.00, 40, 40), (@EvId, N'Estudiante', 6.00, 100, 100);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos WHERE Titulo = N'Encuentro Acústico: Cuerdas de Otoño')
BEGIN
    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Encuentro Acústico: Cuerdas de Otoño', N'Recital íntimo de guitarras clásicas y cellos al aire libre en los jardines del museo, explorando compositores iberoamericanos.', '2026-10-26 00:30:00', N'Museo de Arte · Patio de Esculturas', N'/images/evento-musica.jpg', N'Activo');
    SET @EvId = SCOPE_IDENTITY();
    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES (@EvId, N'General', 12.00, 150, 150), (@EvId, N'VIP', 25.00, 35, 35), (@EvId, N'Estudiante', 7.00, 80, 80);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos WHERE Titulo = N'Noche de Murales y Arte Urbano: Grafos 2026')
BEGIN
    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Noche de Murales y Arte Urbano: Grafos 2026', N'Intervenciones plásticas en vivo, serigrafía participativa y mapping lumínico en los muros históricos del centro cultural.', '2026-10-30 01:30:00', N'Centro Cultural · Galería Abierta', N'/images/evento-arte.jpg', N'Activo');
    SET @EvId = SCOPE_IDENTITY();
    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES (@EvId, N'General', 8.00, 200, 200), (@EvId, N'VIP', 16.00, 50, 50), (@EvId, N'Estudiante', 4.50, 120, 120);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos WHERE Titulo = N'Ópera de Cámara: Voces en el Crepúsculo')
BEGIN
    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Ópera de Cámara: Voces en el Crepúsculo', N'Ensamble vocal y orquesta de cámara interpretan arias selectas y relatos líricos en una velada especial de vísperas.', '2026-11-01 02:00:00', N'Teatro Luis Poma · Sala Principal', N'/images/evento-teatro.jpg', N'Activo');
    SET @EvId = SCOPE_IDENTITY();
    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES (@EvId, N'General', 15.00, 160, 160), (@EvId, N'VIP', 32.00, 45, 45), (@EvId, N'Estudiante', 9.00, 90, 90);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos WHERE Titulo = N'Sinfonía Urbana: Orquesta Filarmónica Joven')
BEGIN
    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Sinfonía Urbana: Orquesta Filarmónica Joven', N'Más de setenta músicos juveniles interpretan obras de Dvořák y arreglos sinfónicos de música contemporánea centroamericana.', '2026-11-07 02:00:00', N'Auditorio Central · Foro Mayor', N'/images/evento-musica.jpg', N'Activo');
    SET @EvId = SCOPE_IDENTITY();
    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES (@EvId, N'General', 14.00, 280, 280), (@EvId, N'VIP', 30.00, 60, 60), (@EvId, N'Estudiante', 8.00, 150, 150);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos WHERE Titulo = N'Muestra Internacional de Cine y Nuevos Medios')
BEGIN
    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Muestra Internacional de Cine y Nuevos Medios', N'Proyección de cortometrajes premiados, paneles con directores invitados y conversatorios sobre lenguaje audiovisual emergente.', '2026-11-20 01:00:00', N'Cinemateca Universitaria · Sala 1', N'/images/evento-arte.jpg', N'Activo');
    SET @EvId = SCOPE_IDENTITY();
    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES (@EvId, N'General', 6.00, 140, 140), (@EvId, N'VIP', 12.00, 30, 30), (@EvId, N'Estudiante', 3.50, 100, 100);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos WHERE Titulo = N'Festival de Jazz & Fusión: Síncopa Libre')
BEGIN
    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Festival de Jazz & Fusión: Síncopa Libre', N'Tríos y cuartetos destacados improvisan jazz modal, ritmos afrocaribeños y armonías modernas bajo las estrellas.', '2026-11-28 02:30:00', N'Plaza de las Artes · Anfiteatro', N'/images/evento-musica.jpg', N'Activo');
    SET @EvId = SCOPE_IDENTITY();
    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES (@EvId, N'General', 16.00, 220, 220), (@EvId, N'VIP', 35.00, 50, 50), (@EvId, N'Estudiante', 10.00, 110, 110);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos WHERE Titulo = N'El Cascanueces: Temporada Navideña')
BEGIN
    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'El Cascanueces: Temporada Navideña', N'El clásico ballet navideño de Tchaikovsky interpretado con escenografía monumental, vestuario artesanal y orquesta en vivo.', '2026-12-06 01:00:00', N'Teatro Presidente · Sala Principal', N'/images/evento-teatro.jpg', N'Activo');
    SET @EvId = SCOPE_IDENTITY();
    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES (@EvId, N'General', 18.00, 300, 300), (@EvId, N'VIP', 42.00, 80, 80), (@EvId, N'Estudiante', 10.00, 160, 160);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos WHERE Titulo = N'Feria de Diseño y Creación Contemporánea')
BEGIN
    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Feria de Diseño y Creación Contemporánea', N'Más de cincuenta talleres editoriales, ilustradores, ceramistas y diseñadores de producto en una feria abierta de fin de año.', '2026-12-12 16:00:00', N'Centro Cultural · Nave Central', N'/images/evento-arte.jpg', N'Activo');
    SET @EvId = SCOPE_IDENTITY();
    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES (@EvId, N'General', 5.00, 350, 350), (@EvId, N'VIP', 12.00, 50, 50), (@EvId, N'Estudiante', 3.00, 200, 200);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Eventos WHERE Titulo = N'Gran Gala Coral y Música Sacra de Fin de Año')
BEGIN
    INSERT INTO dbo.Eventos (Titulo, Descripcion, Fecha, Ubicacion, Imagen, Estado)
    VALUES (N'Gran Gala Coral y Música Sacra de Fin de Año', N'Ciento veinte voces polifónicas y conjunto de metales en un repertorio sublime de música renacentista y villancicos del mundo.', '2026-12-23 02:00:00', N'Catedral Histórica · Nave Mayor', N'/images/evento-musica.jpg', N'Activo');
    SET @EvId = SCOPE_IDENTITY();
    INSERT INTO dbo.TiposEntrada (EventoId, Nombre, Precio, CapacidadTotal, nStockDisponible)
    VALUES (@EvId, N'General', 12.00, 250, 250), (@EvId, N'VIP', 28.00, 60, 60), (@EvId, N'Estudiante', 7.00, 120, 120);
END;
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
