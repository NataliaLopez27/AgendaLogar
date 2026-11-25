using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Importacion;
using ClosedXML.Excel;
using static AgendaLogar.Tests.EscenarioClientes;

namespace AgendaLogar.Tests;

public class AnotacionesTests
{
    [Theory]
    [InlineData("1-1-2026", "2026-01-01")]
    [InlineData("22-7-24", "2024-07-22")]
    [InlineData("30/10/2026", "2026-10-30")]
    [InlineData("31.3.25", "2025-03-31")]
    public void Parsea_las_fechas_escritas_a_mano(string texto, string esperado) =>
        Assert.Equal(F(esperado), Anotaciones.ParsearFecha(texto));

    [Theory]
    [InlineData("31-2-2026")]
    [InlineData("hoy")]
    [InlineData("")]
    public void Rechaza_fechas_imposibles(string texto) => Assert.Null(Anotaciones.ParsearFecha(texto));

    [Fact]
    public void Separa_el_alta_del_nombre()
    {
        var n = Anotaciones.InterpretarNombre("GARCIA PEREZ, JUAN ( A 1-1-2026)");
        Assert.Equal("GARCIA PEREZ, JUAN", n.Nombre);
        Assert.Equal(F("2026-01-01"), n.FechaAlta);
        Assert.Null(n.FechaBaja);
    }

    [Fact]
    public void Separa_la_baja_del_nombre()
    {
        var n = Anotaciones.InterpretarNombre("LOPEZ SL BAJA 30-10-2026");
        Assert.Equal("LOPEZ SL", n.Nombre);
        Assert.Equal(F("2026-10-30"), n.FechaBaja);
    }

    [Theory]
    [InlineData("X", true, null, null, null, false)]
    [InlineData("x", true, null, null, null, false)]
    [InlineData("X(A22-7-24)", true, "2024-07-22", null, null, false)]
    [InlineData("X (A 22-7-24)", true, "2024-07-22", null, null, false)]
    [InlineData("BAJA 30-10-2026", true, null, "2026-10-30", null, false)]
    [InlineData("X BAJA 31-3-25", true, null, "2025-03-31", null, false)]
    [InlineData("X OJO CAMBIAR NUMERO CUENTA EN EL MODELO 111", true, null, null, "OJO CAMBIAR NUMERO CUENTA EN EL MODELO 111", false)]
    [InlineData("arrendamiento", true, null, null, "arrendamiento", true)]
    [InlineData("NO", false, null, null, null, false)]
    [InlineData("", false, null, null, null, false)]
    public void Interpreta_las_celdas_de_modelo(string celda, bool lleva, string? alta, string? baja, string? nota, bool revisar)
    {
        var c = Anotaciones.InterpretarCeldaModelo(celda);

        Assert.Equal(lleva, c.Lleva);
        Assert.Equal(alta is null ? null : F(alta), c.FechaAlta);
        Assert.Equal(baja is null ? null : F(baja), c.FechaBaja);
        Assert.Equal(nota, c.Nota);
        Assert.Equal(revisar, c.Aviso is not null);
    }
}

public class RF60_ImportacionTests
{
    private static readonly IReadOnlyCollection<Modelo> Catalogo = AgendaLogar.Core.Datos.CatalogoModelos.Inicial;

    /// <summary>Cuadrante de personas físicas como los de la gestoría (con título encima de la cabecera).</summary>
    private static MemoryStream CrearCuadranteFisicas()
    {
        using var libro = new XLWorkbook();
        var ws = libro.AddWorksheet("3T 2026");
        ws.Cell(1, 1).Value = "CUADRANTE NURIA - 3T 2026 - PERSONAS FÍSICAS";
        string[] cabecera = { "NOMBRE", "NIF", "130", "131", "111", "115", "303", "349" };
        for (var i = 0; i < cabecera.Length; i++) ws.Cell(3, i + 1).Value = cabecera[i];
        object[][] filas =
        {
            new object[] { "GARCIA PEREZ, JUAN", "12345678Z", "X", "", "X", "", "X", "" },
            new object[] { "LOPEZ RUIZ, ANA ( A 1-1-2026)", "X1234567L", "X", "", "", "arrendamiento", "X(A22-7-26)", "NO" },
            new object[] { "MARTIN SANZ, LUIS BAJA 30-10-2026", "", "", "X", "", "", "BAJA 31-3-26", "" },
            new object[] { "", "", "", "", "", "", "", "" },
            new object[] { "TOTAL", "", "2", "1", "1", "1", "3", "0" }
        };
        for (var f = 0; f < filas.Length; f++)
            for (var c = 0; c < filas[f].Length; c++)
                ws.Cell(4 + f, c + 1).Value = filas[f][c]?.ToString();
        var ms = new MemoryStream();
        libro.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static CuadranteLeido LeerFisicas()
    {
        using var xlsx = CrearCuadranteFisicas();
        var tabla = Cuadrante.LeerTabla(xlsx);
        return Cuadrante.Interpretar(tabla, "CUADRANTE NURIA.xlsx", TipoCliente.Fisica, Catalogo, F("2000-01-01"));
    }

    [Fact]
    public void Lee_el_cuadrante_encuentra_la_cabecera_aunque_haya_un_titulo_y_salta_los_totales()
    {
        var leido = LeerFisicas();

        Assert.Empty(leido.Avisos);
        // La fila vacía y la de "TOTAL" no son clientes.
        Assert.Equal(new[] { "GARCIA PEREZ, JUAN", "LOPEZ RUIZ, ANA", "MARTIN SANZ, LUIS" },
            leido.Clientes.Select(c => c.Nombre));
    }

    [Fact]
    public void Interpreta_modelos_fechas_y_notas_de_cada_cliente()
    {
        var leido = LeerFisicas();
        var juan = leido.Clientes[0];
        var ana = leido.Clientes[1];
        var luis = leido.Clientes[2];

        Assert.Equal(new[] { "111", "130", "303" }, juan.Modelos.Select(m => m.Codigo).OrderBy(x => x));
        Assert.Equal(F("2000-01-01"), juan.FechaAlta);
        Assert.Empty(juan.Avisos);
        Assert.Equal(4, juan.Fila); // fila del Excel

        Assert.Equal(F("2026-01-01"), ana.FechaAlta);
        Assert.Equal(F("2026-07-22"), ana.Modelos.Single(m => m.Codigo == "303").FechaAlta);
        Assert.Equal("arrendamiento", ana.Modelos.Single(m => m.Codigo == "115").Notas);
        Assert.DoesNotContain(ana.Modelos, m => m.Codigo == "349");
        Assert.Contains(ana.Avisos, a => a.Contains("arrendamiento"));

        Assert.Equal(F("2026-10-30"), luis.FechaBaja);
        Assert.Equal(F("2026-03-31"), luis.Modelos.Single(m => m.Codigo == "303").FechaBaja);
        Assert.Contains(luis.Avisos, a => a.Contains("Sin NIF"));
    }

    [Fact]
    public void Avisa_si_no_encuentra_la_cabecera()
    {
        var tabla = new[] { new[] { "Hola", "Adiós" } };

        var leido = Cuadrante.Interpretar(tabla, "raro.xlsx", TipoCliente.Fisica, Catalogo, F("2000-01-01"));

        Assert.Empty(leido.Clientes);
        Assert.Contains("no se ha encontrado la fila de cabecera", leido.Avisos.Single());
    }

    [Fact]
    public void No_importa_modelos_que_no_aplican_al_tipo()
    {
        var tabla = new[]
        {
            new[] { "CLIENTE", "130", "303", "200" },
            new[] { "PEREZ, ANA", "X", "X", "X" }
        };

        var leido = Cuadrante.Interpretar(tabla, "f.xlsx", TipoCliente.Fisica, Catalogo, F("2000-01-01"));

        var ana = leido.Clientes.Single();
        Assert.Equal(new[] { "130", "303" }, ana.Modelos.Select(m => m.Codigo));
        Assert.Contains(ana.Avisos, a => a.Contains("Modelo 200"));
    }

    [Fact]
    public async Task La_simulacion_no_guarda_nada()
    {
        using var e = new EscenarioClientes();
        var importador = new ImportadorClientes(e.Bd);

        var r = await importador.ImportarAsync(LeerFisicas().Clientes, aplicar: false);

        Assert.Equal(3, r.Creados.Count);
        using var db = e.Bd.CreateDbContext();
        Assert.Empty(db.Clientes);
    }

    [Fact]
    public async Task Importa_clientes_altas_bajas_y_modelos()
    {
        using var e = new EscenarioClientes();
        var importador = new ImportadorClientes(e.Bd);

        await importador.ImportarAsync(LeerFisicas().Clientes, aplicar: true);

        var lista = await e.Servicio.ListarAsync();
        var luis = lista.Single(c => c.Nombre == "MARTIN SANZ, LUIS");
        Assert.Equal("Baja 30/10/2026", luis.EstadoTexto);
        Assert.Equal("", luis.Nif);
        Assert.Equal("111, 130, 303", lista.Single(c => c.Nombre == "GARCIA PEREZ, JUAN").Modelos);

        var ana = lista.Single(c => c.Nombre == "LOPEZ RUIZ, ANA");
        var datos = await e.Servicio.ObtenerDatosAsync(ana.Id);
        Assert.StartsWith("Importado del cuadrante. Revisar:", datos.Notas);
    }

    [Fact]
    public async Task Se_puede_repetir_sin_duplicar()
    {
        using var e = new EscenarioClientes();
        var importador = new ImportadorClientes(e.Bd);
        await importador.ImportarAsync(LeerFisicas().Clientes, aplicar: true);

        var segunda = await importador.ImportarAsync(LeerFisicas().Clientes, aplicar: true);

        Assert.Empty(segunda.Creados);
        Assert.Equal(3, segunda.Omitidos.Count);
        Assert.All(segunda.Omitidos, o => Assert.Equal("ya existía en la base de datos", o.Motivo));
        Assert.Equal(3, (await e.Servicio.ListarAsync()).Count);
    }

    [Fact]
    public async Task El_mismo_cliente_en_dos_cuadrantes_se_une_y_suma_sus_modelos()
    {
        using var e = new EscenarioClientes();
        var t3 = Cuadrante.Interpretar(new[]
        {
            new[] { "NOMBRE", "NIF", "303", "111" },
            new[] { "Ana López", "12345678Z", "X", "" }
        }, "3T.xlsx", TipoCliente.Fisica, Catalogo, F("2000-01-01"));
        var t4 = Cuadrante.Interpretar(new[]
        {
            new[] { "NOMBRE", "NIF", "303", "111" },
            new[] { "ANA LOPEZ", "12345678-z", "X", "X" }
        }, "4T.xlsx", TipoCliente.Fisica, Catalogo, F("2000-01-01"));

        var r = await new ImportadorClientes(e.Bd).ImportarAsync(t3.Clientes.Concat(t4.Clientes), aplicar: true);

        Assert.Single(r.Creados);
        Assert.Contains("se añaden 1 modelo", r.Omitidos.Single().Motivo);
        Assert.Equal("111, 303", (await e.Servicio.ListarAsync()).Single().Modelos);
    }

    [Fact]
    public async Task Sin_NIF_se_reconoce_por_nombre_y_tipo_sin_acentos()
    {
        using var e = new EscenarioClientes();
        var a = Cuadrante.Interpretar(new[] { new[] { "NOMBRE", "303", "111" }, new[] { "Ángel Núñez", "X", "" } },
            "a.xlsx", TipoCliente.Fisica, Catalogo, F("2000-01-01"));
        var b = Cuadrante.Interpretar(new[] { new[] { "NOMBRE", "303", "111" }, new[] { "ANGEL NUÑEZ", "X", "" } },
            "b.xlsx", TipoCliente.Fisica, Catalogo, F("2000-01-01"));
        var importador = new ImportadorClientes(e.Bd);

        await importador.ImportarAsync(a.Clientes, aplicar: true);
        var segunda = await importador.ImportarAsync(b.Clientes, aplicar: true);

        Assert.Empty(segunda.Creados);
        Assert.Single(await e.Servicio.ListarAsync());
    }
}
