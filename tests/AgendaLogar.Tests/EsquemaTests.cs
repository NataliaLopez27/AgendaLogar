using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Dominio;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Tests;

public class EsquemaTests
{
    [Fact]
    public void El_seed_carga_el_catalogo_de_modelos_de_la_seccion_3()
    {
        using var bd = new BaseDatosPrueba();
        using var db = bd.CreateDbContext();

        var trimestrales = db.Modelos.Where(m => m.Periodicidad == Periodicidad.Trimestral).Select(m => m.Codigo).ToList();
        var anuales = db.Modelos.Where(m => m.Periodicidad == Periodicidad.Anual).Select(m => m.Codigo).ToList();

        Assert.Equal(new[] { "100", "111", "115", "130", "131", "180", "190", "200", "202", "303", "347", "349", "390" },
            db.Modelos.Select(m => m.Codigo).OrderBy(c => c).ToArray());
        Assert.Equivalent(new[] { "130", "131", "111", "115", "303", "349", "202", "200" }, trimestrales);
        Assert.Equivalent(new[] { "390", "190", "180", "347", "100" }, anuales);
    }

    [Theory]
    [InlineData("130", true, false)]
    [InlineData("303", true, true)]
    [InlineData("200", false, true)]
    [InlineData("100", true, false)]
    public void Los_modelos_aplican_al_tipo_de_cliente_correcto(string codigo, bool fisica, bool sociedad)
    {
        using var bd = new BaseDatosPrueba();
        using var db = bd.CreateDbContext();
        var m = db.Modelos.Single(x => x.Codigo == codigo);
        Assert.Equal(fisica, m.AplicaFisica);
        Assert.Equal(sociedad, m.AplicaSociedad);
    }

    [Fact]
    public void No_se_puede_repetir_una_asignacion_del_mismo_cliente_en_un_periodo()
    {
        using var bd = new BaseDatosPrueba();
        using var db = bd.CreateDbContext();
        var cliente = new Cliente { Nombre = "Cliente", Nif = "12345678Z", Tipo = TipoCliente.Fisica };
        var periodo = new Periodo { Anio = 2026, Trimestre = 4 };
        db.AddRange(cliente, periodo);
        db.SaveChanges();

        db.Asignaciones.Add(new Asignacion { ClienteId = cliente.Id, PeriodoId = periodo.Id });
        db.SaveChanges();
        db.Asignaciones.Add(new Asignacion { ClienteId = cliente.Id, PeriodoId = periodo.Id });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void El_indice_unico_de_periodo_no_tiene_filtro_para_que_el_anual_no_se_repita_en_SqlServer()
    {
        // SQLite trata los NULL como distintos; SQL Server, en un índice único sin filtro, no.
        // Por eso se comprueba la configuración que se genera para SQL Server.
        using var db = new DesignTimeDbContextFactory().CreateDbContext(Array.Empty<string>());
        var indice = db.Model.FindEntityType(typeof(Periodo))!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Anio", "Trimestre" }));

        Assert.True(indice.IsUnique);
        Assert.Null(indice.GetFilter());
    }
}
