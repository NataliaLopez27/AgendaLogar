using AgendaLogar.Core.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgendaLogar.Core.Datos.Configuraciones;

internal class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuario");
        b.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
        b.Property(u => u.NombreUsuario).HasMaxLength(50).IsRequired();
        b.Property(u => u.HashPassword).HasMaxLength(100).IsRequired();
        b.HasIndex(u => u.NombreUsuario).IsUnique();
    }
}
