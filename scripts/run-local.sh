#!/usr/bin/env bash
set -euo pipefail

project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
runtime_dir="${ENTRADA_SQL_RUNTIME:-${HOME}/.local/opt/entrada-sql-runtime}"

if [[ ! -x "${runtime_dir}/bin/colima" ]]; then
    printf 'No se encontró el entorno local de SQL Server.\n' >&2
    exit 1
fi

export PATH="${runtime_dir}/bin:${runtime_dir}/lima/bin:${PATH}"
colima start

if ! colima nerdctl ps -- --format '{{.Names}}' | grep -Fxq 'entrada-sql'; then
    colima nerdctl start entrada-sql
fi

for attempt in {1..45}; do
    if colima nerdctl exec -- entrada-sql sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d EntradaDb -b -Q "SET NOCOUNT ON; SELECT 1" >/dev/null' >/dev/null 2>&1; then
        break
    fi
    if (( attempt == 45 )); then
        printf 'SQL Server o EntradaDb no respondió en el entorno local.\n' >&2
        exit 1
    fi
    sleep 1
done

cd "${project_dir}"
dotnet build --nologo
exec swift scripts/launch-app.swift
