namespace AgendaLogar.Core.Dominio;

/// <summary>Un trimestre (1T–4T) de un año, o el ejercicio anual si <see cref="Trimestre"/> es nulo.</summary>
public class Periodo
{
    public int Id { get; set; }
    public int Anio { get; set; }

    /// <summary>1–4 para trimestrales; nulo para el periodo anual.</summary>
    public byte? Trimestre { get; set; }

    public EstadoPeriodo Estado { get; set; } = EstadoPeriodo.Abierto;

    public ICollection<Asignacion> Asignaciones { get; set; } = new List<Asignacion>();

    public bool EsAnual => Trimestre is null;

    /// <summary>Primer día del periodo (RN-01).</summary>
    public DateOnly FechaInicio => Trimestre is byte t
        ? new DateOnly(Anio, (t - 1) * 3 + 1, 1)
        : new DateOnly(Anio, 1, 1);

    /// <summary>Último día del periodo (RN-01).</summary>
    public DateOnly FechaFin => Trimestre is byte t
        ? new DateOnly(Anio, t * 3, 1).AddMonths(1).AddDays(-1)
        : new DateOnly(Anio, 12, 31);

    /// <summary>Nombre corto, por ejemplo "3T 2026" o "Anual 2026".</summary>
    public string Nombre => Trimestre is byte t ? $"{t}T {Anio}" : $"Anual {Anio}";
}
