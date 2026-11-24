using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgendaLogar.Core.Datos;

/// <summary>
/// Lo usan las herramientas de EF Core (dotnet ef / Consola del Administrador de paquetes)
/// para crear migraciones y actualizar la base de datos de desarrollo (LocalDB).
/// Se puede usar otra cadena con la variable de entorno AGENDALOGAR_CONNECTION.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AgendaLogarDbContext>
{
    public const string CadenaLocalDb =
        "Server=(localdb)\\MSSQLLocalDB;Database=AgendaLogar;Trusted_Connection=True;TrustServerCertificate=True";

    public AgendaLogarDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("AGENDALOGAR_CONNECTION") ?? CadenaLocalDb;
        var options = new DbContextOptionsBuilder<AgendaLogarDbContext>()
            .UseSqlServer(cadena)
            .Options;
        return new AgendaLogarDbContext(options);
    }
}
