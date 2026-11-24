using AgendaLogar.Core.Dominio;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Core.Datos;

/// <summary>Contexto de Entity Framework Core con el esquema de la sección 6 de los requisitos.</summary>
public class AgendaLogarDbContext : DbContext
{
    public AgendaLogarDbContext(DbContextOptions<AgendaLogarDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<AltaCliente> AltasCliente => Set<AltaCliente>();
    public DbSet<Modelo> Modelos => Set<Modelo>();
    public DbSet<ClienteModelo> ClienteModelos => Set<ClienteModelo>();
    public DbSet<Periodo> Periodos => Set<Periodo>();
    public DbSet<Asignacion> Asignaciones => Set<Asignacion>();
    public DbSet<HistorialAsignacion> HistorialAsignaciones => Set<HistorialAsignacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgendaLogarDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Los enumerados se guardan como texto para que la base de datos sea legible.
        configurationBuilder.Properties<Rol>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<TipoCliente>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<Periodicidad>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<EstadoPeriodo>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<EstadoAsignacion>().HaveConversion<string>().HaveMaxLength(20);
    }
}
