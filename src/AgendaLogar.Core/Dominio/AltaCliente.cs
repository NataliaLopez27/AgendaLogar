namespace AgendaLogar.Core.Dominio;

/// <summary>Un tramo de alta de un cliente. Si <see cref="FechaBaja"/> es nula, sigue de alta.</summary>
public class AltaCliente
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public DateOnly FechaAlta { get; set; }
    public DateOnly? FechaBaja { get; set; }

    public Cliente Cliente { get; set; } = null!;
}
