using AgendaLogar.Core.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgendaLogar.Core.Datos.Configuraciones;

internal class ModeloConfiguracion : IEntityTypeConfiguration<Modelo>
{
    public void Configure(EntityTypeBuilder<Modelo> b)
    {
        b.ToTable("Modelo");
        b.HasKey(m => m.Codigo);
        b.Property(m => m.Codigo).HasMaxLength(10);
        b.Property(m => m.Descripcion).HasMaxLength(200).IsRequired();
        b.Property(m => m.ResumeA).HasMaxLength(10);

        // Seed del catálogo inicial de modelos fiscales.
        b.HasData(CatalogoModelos.Inicial.Select(m => new
        {
            m.Codigo,
            m.Descripcion,
            m.Periodicidad,
            m.AplicaFisica,
            m.AplicaSociedad,
            m.Orden,
            m.ResumeA
        }));
    }
}
