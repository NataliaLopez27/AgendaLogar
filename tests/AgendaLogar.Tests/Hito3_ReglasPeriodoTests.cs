using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Dominio;
using static AgendaLogar.Tests.EscenarioClientes;

namespace AgendaLogar.Tests;

/// <summary>Issue #24: RN-01…RN-05 y RF-44.</summary>
public class Hito3_ReglasPeriodoTests
{
    private static readonly IReadOnlyDictionary<string, Modelo> Catalogo = CatalogoModelos.Inicial.ToDictionary(m => m.Codigo);
    private static Periodo T(int anio, byte t) => new() { Anio = anio, Trimestre = t };
    private static Periodo Anual(int anio) => new() { Anio = anio };

    private static Cliente Cliente(TipoCliente tipo = TipoCliente.Fisica, params (string alta, string? baja)[] altas)
    {
        var c = new Cliente { Nombre = "X", Nif = "12345678Z", Tipo = tipo };
        foreach (var (alta, baja) in altas)
            c.Altas.Add(new AltaCliente { FechaAlta = F(alta), FechaBaja = baja is null ? null : F(baja) });
        return c;
    }

    private static void Modelo(Cliente c, string codigo, string alta, string? baja = null) =>
        c.Modelos.Add(new ClienteModelo { ModeloCodigo = codigo, FechaAlta = F(alta), FechaBaja = baja is null ? null : F(baja) });

    [Fact]
    public void Ejemplo_del_documento_baja_el_24_11_2026()
    {
        var c = Cliente(altas: ("2020-01-01", "2026-11-24"));
        Modelo(c, "303", "2020-01-01");
        Modelo(c, "390", "2020-01-01");

        Assert.True(ReglasPeriodo.EntraEnPeriodo(c, T(2026, 4), Catalogo));
        Assert.False(ReglasPeriodo.EntraEnPeriodo(c, T(2027, 1), Catalogo));
        Assert.True(ReglasPeriodo.EntraEnPeriodo(c, Anual(2026), Catalogo));
        Assert.False(ReglasPeriodo.EntraEnPeriodo(c, Anual(2027), Catalogo));
    }

    [Fact]
    public void RN05_etiqueta_de_baja_solo_en_su_ultimo_periodo()
    {
        var c = Cliente(altas: ("2020-01-01", "2026-11-24"));

        Assert.Equal(F("2026-11-24"), ReglasPeriodo.BajaDentroDelPeriodo(c, T(2026, 4)));
        Assert.Null(ReglasPeriodo.BajaDentroDelPeriodo(c, T(2026, 3)));
    }

    [Fact]
    public void RN05_sin_etiqueta_si_se_reactiva_dentro_del_mismo_periodo()
    {
        var c = Cliente(altas: new[] { ("2020-01-01", (string?)"2026-10-15"), ("2026-11-20", null) });

        Assert.True(ReglasPeriodo.EntraEnPeriodo(c, T(2026, 4), Catalogo));
        Assert.Null(ReglasPeriodo.BajaDentroDelPeriodo(c, T(2026, 4)));
    }

    [Fact]
    public void RN02_solo_se_muestran_los_modelos_vigentes_y_de_la_periodicidad_del_periodo()
    {
        var c = Cliente(altas: ("2020-01-01", null));
        Modelo(c, "303", "2020-01-01", "2025-03-31"); // baja IVA 31-3-25
        Modelo(c, "130", "2020-01-01");
        Modelo(c, "115", "2025-07-17");               // alta en 115 a mitad del 3T
        Modelo(c, "390", "2020-01-01");

        Assert.Equal(new[] { "130", "303" }, ReglasPeriodo.ModelosVigentes(c, T(2025, 1), Catalogo));
        Assert.Equal(new[] { "130" }, ReglasPeriodo.ModelosVigentes(c, T(2025, 2), Catalogo));
        Assert.Equal(new[] { "130", "115" }, ReglasPeriodo.ModelosVigentes(c, T(2025, 3), Catalogo));
        Assert.Equal(new[] { "390" }, ReglasPeriodo.ModelosVigentes(c, Anual(2025), Catalogo));
    }

    [Fact]
    public void RF44_en_el_anual_solo_entran_clientes_con_algun_modelo_anual()
    {
        var sinAnuales = Cliente(altas: ("2020-01-01", null));
        Modelo(sinAnuales, "130", "2020-01-01");
        var conRenta = Cliente(altas: ("2020-01-01", null));
        Modelo(conRenta, "100", "2020-01-01");

        Assert.False(ReglasPeriodo.EntraEnPeriodo(sinAnuales, Anual(2026), Catalogo));
        Assert.True(ReglasPeriodo.EntraEnPeriodo(sinAnuales, T(2026, 4), Catalogo));
        Assert.True(ReglasPeriodo.EntraEnPeriodo(conRenta, Anual(2026), Catalogo));
    }

    [Fact]
    public void Los_modelos_que_no_aplican_al_tipo_no_se_muestran()
    {
        var sociedad = Cliente(TipoCliente.Sociedad, ("2020-01-01", null));
        Modelo(sociedad, "130", "2020-01-01"); // dato erróneo: el 130 es de personas físicas
        Modelo(sociedad, "202", "2020-01-01");

        Assert.Equal(new[] { "202" }, ReglasPeriodo.ModelosVigentes(sociedad, T(2026, 4), Catalogo));
    }

    [Theory]
    [InlineData(2026, (byte)3, 2026, (byte)2)]
    [InlineData(2026, (byte)1, 2025, (byte)4)]
    public void El_periodo_anterior_de_un_trimestre(int anio, byte t, int anioEsperado, byte tEsperado) =>
        Assert.Equal((anioEsperado, (byte?)tEsperado), ReglasPeriodo.Anterior(anio, t));

    [Fact]
    public void El_periodo_anterior_del_anual_es_el_anual_del_año_anterior() =>
        Assert.Equal((2025, (byte?)null), ReglasPeriodo.Anterior(2026, null));
}
