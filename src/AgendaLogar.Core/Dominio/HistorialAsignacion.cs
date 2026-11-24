namespace AgendaLogar.Core.Dominio;

/// <summary>Auditoría de cambios de estado, importe u observaciones (RNF-05).</summary>
public class HistorialAsignacion
{
    public long Id { get; set; }
    public int AsignacionId { get; set; }
    public string Campo { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public int UsuarioId { get; set; }
    public DateTime Fecha { get; set; }

    public Asignacion Asignacion { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
