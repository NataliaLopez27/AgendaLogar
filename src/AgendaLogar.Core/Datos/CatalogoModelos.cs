using AgendaLogar.Core.Dominio;

namespace AgendaLogar.Core.Datos;

/// <summary>Catálogo inicial de modelos fiscales (sección 3 de los requisitos).</summary>
public static class CatalogoModelos
{
    public static IReadOnlyList<Modelo> Inicial { get; } = new List<Modelo>
    {
        // 3.1 Trimestrales
        T("130", "Pago fraccionado IRPF (estimación directa)", fisica: true, sociedad: false, orden: 10),
        T("131", "Pago fraccionado IRPF (módulos)", fisica: true, sociedad: false, orden: 20),
        T("111", "Retenciones trabajo / profesionales", fisica: true, sociedad: true, orden: 30),
        T("115", "Retenciones alquileres", fisica: true, sociedad: true, orden: 40),
        T("303", "IVA", fisica: true, sociedad: true, orden: 50),
        T("349", "Operaciones intracomunitarias", fisica: true, sociedad: true, orden: 60),
        T("202", "Pago fraccionado Impuesto de Sociedades", fisica: false, sociedad: true, orden: 70),
        T("200", "Impuesto de Sociedades", fisica: false, sociedad: true, orden: 80),

        // 3.2 Anuales
        A("390", "Resumen anual IVA", resumeA: "303", fisica: true, sociedad: true, orden: 110),
        A("190", "Resumen anual retenciones", resumeA: "111", fisica: true, sociedad: true, orden: 120),
        A("180", "Resumen anual alquileres", resumeA: "115", fisica: true, sociedad: true, orden: 130),
        A("347", "Operaciones con terceros", resumeA: null, fisica: true, sociedad: true, orden: 140),
        A("100", "Declaración de la Renta", resumeA: null, fisica: true, sociedad: false, orden: 150),
    };

    private static Modelo T(string codigo, string descripcion, bool fisica, bool sociedad, int orden) => new()
    {
        Codigo = codigo,
        Descripcion = descripcion,
        Periodicidad = Periodicidad.Trimestral,
        AplicaFisica = fisica,
        AplicaSociedad = sociedad,
        Orden = orden
    };

    private static Modelo A(string codigo, string descripcion, string? resumeA, bool fisica, bool sociedad, int orden) => new()
    {
        Codigo = codigo,
        Descripcion = descripcion,
        Periodicidad = Periodicidad.Anual,
        ResumeA = resumeA,
        AplicaFisica = fisica,
        AplicaSociedad = sociedad,
        Orden = orden
    };
}
