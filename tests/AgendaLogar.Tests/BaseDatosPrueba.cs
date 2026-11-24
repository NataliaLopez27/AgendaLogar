using AgendaLogar.Core.Datos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Tests;

/// <summary>
/// Base de datos SQLite en memoria con el mismo modelo que la real.
/// Sirve para probar el esquema y los servicios sin SQL Server.
/// </summary>
public sealed class BaseDatosPrueba : IDbContextFactory<AgendaLogarDbContext>, IDisposable
{
    private readonly SqliteConnection _conexion;
    private readonly DbContextOptions<AgendaLogarDbContext> _opciones;

    public BaseDatosPrueba()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _opciones = new DbContextOptionsBuilder<AgendaLogarDbContext>().UseSqlite(_conexion).Options;
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public AgendaLogarDbContext CreateDbContext() => new(_opciones);

    public void Dispose() => _conexion.Dispose();
}
