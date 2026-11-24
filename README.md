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

## Estado

- **Hito 0 — Arranque**: solución, base MVVM, inyección de dependencias, navegación, esquema EF Core y seed de modelos. ✔
- **Hito 1 — Autenticación**: CU-01 inicio/cierre de sesión, roles y control de acceso, CU-02 cambiar contraseña, CU-10 gestión de empleados. ✔
