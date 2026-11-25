# AgendaLogar
Repositorio con la creación de un sistema que permita organizar a los diferentes empleados, de manera que tengan un control de sus tareas.

Los requisitos y la planificación están en [`docs/requisitos.md`](docs/requisitos.md).

## Estructura

| Proyecto | Qué contiene |
|---|---|
| `src/AgendaLogar.App` | Aplicación de escritorio **WPF (.NET 8)**: vistas XAML, tema y logo, arranque con inyección de dependencias. |
| `src/AgendaLogar.Presentacion` | ViewModels (MVVM con CommunityToolkit.Mvvm) y navegación. No depende de WPF, así se puede probar. |
| `src/AgendaLogar.Core` | Dominio, base de datos (EF Core + migraciones), seguridad y servicios de negocio. |
| `tests/AgendaLogar.Tests` | Tests unitarios (xUnit) con SQLite en memoria. |
| `tools/AgendaLogar.Importador` | Importación de clientes desde los cuadrantes Excel (RF-60). |

## Cómo arrancarla (Windows + Visual Studio 2022)

1. Abre `AgendaLogar.sln` y pon **AgendaLogar.App** como proyecto de inicio.
2. Pulsa **F5**. La primera vez crea la base de datos `AgendaLogar` en **LocalDB** y aplica las migraciones
   (`appsettings.json` → `BaseDatos:AplicarMigracionesAlIniciar`).
3. Entra con **`admin` / `admin`**. La aplicación te pedirá una contraseña nueva.

Tests: `dotnet test` (o el Explorador de pruebas de Visual Studio).

Para crear una migración nueva:

```
dotnet ef migrations add NombreMigracion --project src/AgendaLogar.Core --output-dir Datos/Migraciones
```

## Importar los clientes desde los cuadrantes Excel (RF-60)

Primero se hace una **simulación**, que no guarda nada y muestra un informe con lo que hay que revisar:

```
dotnet run --project tools/AgendaLogar.Importador -- "C:\Cuadrantes\Fisicas" --tipo fisica
```

Si el informe es correcto, se repite con `--aplicar`. Se puede ejecutar varias veces: los clientes que ya existen no se duplican.

| Opción | Para qué sirve |
|---|---|
| `--tipo fisica\|sociedad` | Tipo de los clientes (si no, se deduce del nombre del archivo). |
| `--hoja <nombre>` | Hoja del libro (por defecto, la primera). |
| `--fecha-alta <d-m-aaaa>` | Fecha de alta cuando el cuadrante no la indica (por defecto 1-1-2000). |
| `--conexion "<cadena>"` | Base de datos (por defecto, LocalDB o la variable `AGENDALOGAR_CONNECTION`). |
| `--aplicar` | Guarda los cambios. |

Se interpreta: `X` (lleva el modelo), `X(A22-7-24)` (desde esa fecha), `( A 1-1-2026)` en el nombre (alta),
`BAJA 30-10-2026` (baja), `NO` (no lo lleva) y cualquier otro texto como nota (avisando para revisarlo).
Los clientes con avisos llevan en sus notas lo que hay que revisar.

## Estado

- **Hito 0 — Arranque**: solución, base MVVM, inyección de dependencias, navegación, esquema EF Core y seed de modelos. ✔
- **Hito 1 — Autenticación**: CU-01 inicio/cierre de sesión, roles y control de acceso, CU-02 cambiar contraseña, CU-10 gestión de empleados. ✔
- **Hito 2 — Clientes**: CU-11 alta, CU-12 edición, CU-13 baja/reactivación, CU-14 modelos por cliente, RF-25 listado y búsqueda, RF-60 importación desde Excel. ✔
