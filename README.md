# ENTRADA

Aplicación web ASP.NET Core MVC para consultar eventos, reservar entradas y administrar reservas. Usa SQL Server para almacenar usuarios, eventos, entradas y reservas.

## Requisitos

- .NET 8 SDK
- SQL Server

## Configuración local

1. Crea la base de datos y sus datos iniciales ejecutando `Database/entrada.sql` en SQL Server.
2. Configura la cadena `ConnectionStrings:Entrada` con User Secrets o la variable de entorno `ConnectionStrings__Entrada`.
3. Opcionalmente, configura `BootstrapAdmin__Email` y `BootstrapAdmin__Password` mediante User Secrets o variables de entorno para crear el administrador inicial.
4. Inicia la aplicación:

   ```sh
   dotnet run
   ```

No guardes credenciales en los archivos del repositorio. En desarrollo también se puede usar `scripts/run-local.sh` si el entorno local de SQL Server del proyecto está instalado.
