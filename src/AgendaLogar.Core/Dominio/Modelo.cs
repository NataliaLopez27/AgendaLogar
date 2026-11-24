namespace AgendaLogar.Core.Dominio;

/// <summary>Modelo fiscal (303, 111, 130…). Tabla configurable (sección 3).</summary>
public class Modelo
{
    /// <summary>Código del modelo, por ejemplo "303".</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;
    public Periodicidad Periodicidad { get; set; }
    public bool AplicaFisica { get; set; }
    public bool AplicaSociedad { get; set; }

    /// <summary>Orden de las columnas en los listados.</summary>
    public int Orden { get; set; }

    /// <summary>Para los anuales, modelo trimestral que resumen (390 → 303). Solo informativo.</summary>
    public string? ResumeA { get; set; }

    public ICollection<ClienteModelo> Clientes { get; set; } = new List<ClienteModelo>();
}
