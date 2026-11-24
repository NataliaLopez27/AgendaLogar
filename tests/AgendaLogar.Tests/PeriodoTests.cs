using AgendaLogar.Core.Dominio;

namespace AgendaLogar.Tests;

public class PeriodoTests
{
    [Theory]
    [InlineData(1, "2026-01-01", "2026-03-31")]
    [InlineData(2, "2026-04-01", "2026-06-30")]
    [InlineData(3, "2026-07-01", "2026-09-30")]
    [InlineData(4, "2026-10-01", "2026-12-31")]
    public void Las_fechas_de_cada_trimestre_son_las_de_RN01(byte trimestre, string inicio, string fin)
    {
        var p = new Periodo { Anio = 2026, Trimestre = trimestre };
        Assert.Equal(DateOnly.Parse(inicio), p.FechaInicio);
        Assert.Equal(DateOnly.Parse(fin), p.FechaFin);
        Assert.Equal($"{trimestre}T 2026", p.Nombre);
    }

    [Fact]
    public void El_periodo_anual_va_de_enero_a_diciembre()
    {
        var p = new Periodo { Anio = 2026 };
        Assert.True(p.EsAnual);
        Assert.Equal(new DateOnly(2026, 1, 1), p.FechaInicio);
        Assert.Equal(new DateOnly(2026, 12, 31), p.FechaFin);
        Assert.Equal("Anual 2026", p.Nombre);
    }
}
