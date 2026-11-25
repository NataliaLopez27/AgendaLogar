namespace AgendaLogar.Core.Dominio;

/// <summary>
/// Qué clientes y qué modelos entran en un periodo (RN-01, RN-02, RN-05 y RF-44).
/// Trabaja con clientes que tienen cargados sus tramos de alta (<see cref="Cliente.Altas"/>)
/// y de modelos (<see cref="Cliente.Modelos"/>).
/// </summary>
public static class ReglasPeriodo
{
    /// <summary>
    /// Modelos que se muestran del cliente en el periodo (RN-02): los de la periodicidad del periodo
    /// (trimestrales o anuales), que se aplican a su tipo y que han estado de alta al menos un día dentro de él.
    /// </summary>
    public static IReadOnlyList<string> ModelosVigentes(Cliente cliente, Periodo periodo, IReadOnlyDictionary<string, Modelo> catalogo)
    {
        var periodicidad = periodo.EsAnual ? Periodicidad.Anual : Periodicidad.Trimestral;
        return cliente.Modelos
            .Where(m => catalogo.TryGetValue(m.ModeloCodigo, out var modelo)
                        && modelo.Periodicidad == periodicidad
                        && (cliente.Tipo == TipoCliente.Fisica ? modelo.AplicaFisica : modelo.AplicaSociedad))
            .Where(m => Vigencia.EsVigente(m.FechaAlta, m.FechaBaja, periodo))
            .Select(m => m.ModeloCodigo)
            .Distinct()
            .OrderBy(c => catalogo[c].Orden)
            .ToList();
    }

    /// <summary>
    /// RN-01 y RF-44: el cliente entra en el periodo si ha estado de alta al menos un día dentro de él.
    /// En un periodo anual, además, tiene que tener al menos un modelo anual vigente.
    /// </summary>
    public static bool EntraEnPeriodo(Cliente cliente, Periodo periodo, IReadOnlyDictionary<string, Modelo> catalogo) =>
        Vigencia.ClienteVigente(cliente.Altas, periodo)
        && (!periodo.EsAnual || ModelosVigentes(cliente, periodo, catalogo).Count > 0);

    /// <summary>
    /// RN-05: fecha de baja si el cliente se da de baja dentro del periodo (es su último periodo), o null.
    /// Si se reactivó después dentro del mismo periodo, no es su último periodo.
    /// </summary>
    public static DateOnly? BajaDentroDelPeriodo(Cliente cliente, Periodo periodo)
    {
        var ultimo = cliente.Altas
            .Where(a => Vigencia.EsVigente(a.FechaAlta, a.FechaBaja, periodo))
            .OrderByDescending(a => a.FechaAlta)
            .FirstOrDefault();
        return ultimo?.FechaBaja is DateOnly b && b >= periodo.FechaInicio && b <= periodo.FechaFin ? b : null;
    }

    /// <summary>Periodo anterior del mismo tipo: el trimestre anterior (1T → 4T del año anterior) o el año anterior.</summary>
    public static (int Anio, byte? Trimestre) Anterior(int anio, byte? trimestre) => trimestre switch
    {
        null => (anio - 1, null),
        1 => (anio - 1, (byte)4),
        byte t => (anio, (byte)(t - 1))
    };
}
