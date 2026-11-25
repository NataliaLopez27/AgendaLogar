namespace AgendaLogar.Core.Dominio;

/// <summary>
/// Regla de vigencia RN-01 / RN-02: un cliente (o un modelo del cliente) entra en un periodo
/// si ha estado de alta al menos un día dentro de él.
/// </summary>
public static class Vigencia
{
    /// <summary>fecha_alta ≤ fin_periodo y (fecha_baja vacía o fecha_baja ≥ inicio_periodo).</summary>
    public static bool EsVigente(DateOnly fechaAlta, DateOnly? fechaBaja, DateOnly inicio, DateOnly fin) =>
        fechaAlta <= fin && (fechaBaja is null || fechaBaja >= inicio);

    public static bool EsVigente(DateOnly fechaAlta, DateOnly? fechaBaja, Periodo periodo) =>
        EsVigente(fechaAlta, fechaBaja, periodo.FechaInicio, periodo.FechaFin);

    /// <summary>RN-01: el cliente está vigente en el periodo si alguno de sus tramos de alta lo está.</summary>
    public static bool ClienteVigente(IEnumerable<AltaCliente> altas, Periodo periodo) =>
        altas.Any(a => EsVigente(a.FechaAlta, a.FechaBaja, periodo));

    /// <summary>RN-02: el modelo está vigente para el cliente en el periodo si alguno de sus tramos lo está.</summary>
    public static bool ModeloVigente(IEnumerable<ClienteModelo> tramos, string codigoModelo, Periodo periodo) =>
        tramos.Any(t => t.ModeloCodigo == codigoModelo && EsVigente(t.FechaAlta, t.FechaBaja, periodo));
}
