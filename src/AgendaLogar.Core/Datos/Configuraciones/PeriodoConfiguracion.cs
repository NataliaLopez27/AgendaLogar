using AgendaLogar.Core.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgendaLogar.Core.Datos.Configuraciones;

internal class PeriodoConfiguracion : IEntityTypeConfiguration<Periodo>
{
    public void Configure(EntityTypeBuilder<Periodo> b)
    {
        b.ToTable("Periodo", t =>
            t.HasCheckConstraint("CK_Periodo_Trimestre", "[Trimestre] IS NULL OR [Trimestre] BETWEEN 1 AND 4"));

        b.Ignore(p => p.EsAnual);
        b.Ignore(p => p.FechaInicio);
        b.Ignore(p => p.FechaFin);
        b.Ignore(p => p.Nombre);

        // Un único periodo por año y trimestre. HasFilter(null) quita el filtro
        // "IS NOT NULL" que EF añade por defecto, para que tampoco se repita el anual.
        b.HasIndex(p => new { p.Anio, p.Trimestre })
            .IsUnique()
            .HasFilter(null);
    }
}
