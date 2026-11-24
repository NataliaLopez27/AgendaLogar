namespace AgendaLogar.Core.Dominio;

/// <summary>
/// Un tramo en el que un cliente lleva un modelo (por ejemplo, "baja IVA 31-3-25").
/// Un cliente puede tener varios tramos del mismo modelo.
/// </summary>
public class ClienteModelo
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string ModeloCodigo { get; set; } = string.Empty;
    public DateOnly FechaAlta { get; set; }
    public DateOnly? FechaBaja { get; set; }
    public string? Notas { get; set; }

    public Cliente Cliente { get; set; } = null!;
    public Modelo Modelo { get; set; } = null!;
}
