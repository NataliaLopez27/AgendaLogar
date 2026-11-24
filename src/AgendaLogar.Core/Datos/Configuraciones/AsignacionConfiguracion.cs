using AgendaLogar.Core.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgendaLogar.Core.Datos.Configuraciones;

internal class AsignacionConfiguracion : IEntityTypeConfiguration<Asignacion>
{
    public void Configure(EntityTypeBuilder<Asignacion> b)
    {
        b.ToTable("Asignacion");
        b.Property(a => a.Importe).HasPrecision(14, 2);
        b.Property(a => a.Observaciones).HasMaxLength(2000);

        b.HasOne(a => a.Periodo)
            .WithMany(p => p.Asignaciones)
            .HasForeignKey(a => a.PeriodoId)
            .OnDelete(DeleteBehavior.Cascade);

        // RF-26: un cliente con asignaciones no se puede borrar.
        b.HasOne(a => a.Cliente)
            .WithMany(c => c.Asignaciones)
            .HasForeignKey(a => a.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(a => a.Usuario)
            .WithMany(u => u.Asignaciones)
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(a => a.ModificadoPor)
            .WithMany()
            .HasForeignKey(a => a.ModificadoPorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Única por (periodo, cliente).
        b.HasIndex(a => new { a.PeriodoId, a.ClienteId }).IsUnique();
        b.HasIndex(a => new { a.PeriodoId, a.UsuarioId, a.Estado });
    }
}

internal class HistorialAsignacionConfiguracion : IEntityTypeConfiguration<HistorialAsignacion>
{
    public void Configure(EntityTypeBuilder<HistorialAsignacion> b)
    {
        b.ToTable("HistorialAsignacion");
        b.Property(h => h.Campo).HasMaxLength(50).IsRequired();
        b.Property(h => h.ValorAnterior).HasMaxLength(2000);
        b.Property(h => h.ValorNuevo).HasMaxLength(2000);

        b.HasOne(h => h.Asignacion)
            .WithMany(a => a.Historial)
            .HasForeignKey(h => h.AsignacionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(h => h.Usuario)
            .WithMany()
            .HasForeignKey(h => h.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(h => new { h.AsignacionId, h.Fecha });
    }
}
