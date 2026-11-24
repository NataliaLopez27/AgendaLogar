namespace AgendaLogar.Core.Dominio;

/// <summary>"En el periodo P, el cliente C lo lleva el empleado E", con su estado e importe.</summary>
public class Asignacion
{
    public int Id { get; set; }
    public int PeriodoId { get; set; }
    public int ClienteId { get; set; }

    /// <summary>Empleado asignado; nulo = sin asignar (RF-43).</summary>
    public int? UsuarioId { get; set; }

    public EstadoAsignacion Estado { get; set; } = EstadoAsignacion.Pendiente;

    /// <summary>Importe del cliente en el periodo. Puede ser 0 o negativo (CU-05).</summary>
    public decimal? Importe { get; set; }

    public string? Observaciones { get; set; }
    public DateTime? FechaHecho { get; set; }
    public DateTime? FechaListo { get; set; }
    public int? ModificadoPorId { get; set; }
    public DateTime? ModificadoEn { get; set; }

    public Periodo Periodo { get; set; } = null!;
    public Cliente Cliente { get; set; } = null!;
    public Usuario? Usuario { get; set; }
    public Usuario? ModificadoPor { get; set; }

    public ICollection<HistorialAsignacion> Historial { get; set; } = new List<HistorialAsignacion>();
}
