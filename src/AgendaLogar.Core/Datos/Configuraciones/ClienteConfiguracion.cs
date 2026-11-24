using AgendaLogar.Core.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgendaLogar.Core.Datos.Configuraciones;

internal class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Cliente");
        b.Property(c => c.Nombre).HasMaxLength(200).IsRequired();
        b.Property(c => c.Nif).HasMaxLength(20).IsRequired();
        b.Property(c => c.Notas).HasMaxLength(2000);

        // No es único: CU-11 solo avisa si el NIF está duplicado.
        b.HasIndex(c => c.Nif);
        b.HasIndex(c => c.Nombre);
    }
}

internal class AltaClienteConfiguracion : IEntityTypeConfiguration<AltaCliente>
{
    public void Configure(EntityTypeBuilder<AltaCliente> b)
    {
        b.ToTable("AltaCliente");
        b.HasOne(a => a.Cliente)
            .WithMany(c => c.Altas)
            .HasForeignKey(a => a.ClienteId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(a => new { a.ClienteId, a.FechaAlta });
    }
}

internal class ClienteModeloConfiguracion : IEntityTypeConfiguration<ClienteModelo>
{
    public void Configure(EntityTypeBuilder<ClienteModelo> b)
    {
        b.ToTable("ClienteModelo");
        b.Property(cm => cm.ModeloCodigo).HasMaxLength(10).IsRequired();
        b.Property(cm => cm.Notas).HasMaxLength(500);

        b.HasOne(cm => cm.Cliente)
            .WithMany(c => c.Modelos)
            .HasForeignKey(cm => cm.ClienteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un modelo en uso no se puede borrar del catálogo.
        b.HasOne(cm => cm.Modelo)
            .WithMany(m => m.Clientes)
            .HasForeignKey(cm => cm.ModeloCodigo)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(cm => new { cm.ClienteId, cm.ModeloCodigo });
    }
}
