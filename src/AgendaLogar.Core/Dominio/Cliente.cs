namespace AgendaLogar.Core.Dominio;

/// <summary>Persona física o sociedad a la que la gestoría presenta impuestos.</summary>
public class Cliente
{
    public int Id { get; set; }

    /// <summary>Nombre o razón social.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>NIF/CIF.</summary>
    public string Nif { get; set; } = string.Empty;

    public TipoCliente Tipo { get; set; }
    public string? Notas { get; set; }

    /// <summary>Historial de altas y bajas (permite reactivaciones, CU-13).</summary>
    public ICollection<AltaCliente> Altas { get; set; } = new List<AltaCliente>();

    /// <summary>Modelos que lleva el cliente, con sus fechas (RF-24).</summary>
    public ICollection<ClienteModelo> Modelos { get; set; } = new List<ClienteModelo>();

    public ICollection<Asignacion> Asignaciones { get; set; } = new List<Asignacion>();
}
