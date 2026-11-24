using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.ViewModels;
using static AgendaLogar.Tests.EscenarioClientes;

namespace AgendaLogar.Tests;

public class RF25_ListadoClientesTests
{
    /// <summary>
    /// Ana López (física, activa, la llevó Nuria en 3T y Javi en 4T 2026),
    /// Ángel Núñez (física, de baja, sin asignar) y Construcciones SL (sociedad, activa, la lleva Nuria).
    /// </summary>
    private static async Task<(EscenarioClientes e, Usuario nuria, Usuario javi)> Preparar()
    {
        var e = new EscenarioClientes();
        var nuria = Datos.CrearUsuario(e.Bd, "nuria", "secreta123", nombre: "Nuria");
        var javi = Datos.CrearUsuario(e.Bd, "javi", "secreta123", nombre: "Javi");

        var ana = await e.CrearFisica("Ana López", "12345678Z");
        var angel = await e.CrearFisica("Ángel Núñez", "X1234567L");
        await e.Servicio.DarDeBajaAsync(angel.Id, F("2026-10-30"));
        var sl = await e.Servicio.CrearAsync(new DatosCliente("Construcciones SL", "B12345674", TipoCliente.Sociedad, null),
            F("2026-01-01"), Array.Empty<ModeloAlta>());

        using var db = e.Bd.CreateDbContext();
        var t3 = new Periodo { Anio = 2026, Trimestre = 3 };
        var t4 = new Periodo { Anio = 2026, Trimestre = 4 };
        db.Periodos.AddRange(t3, t4);
        db.SaveChanges();
        db.Asignaciones.AddRange(
            new Asignacion { PeriodoId = t3.Id, ClienteId = ana.Id, UsuarioId = nuria.Id },
            new Asignacion { PeriodoId = t4.Id, ClienteId = ana.Id, UsuarioId = javi.Id },
            new Asignacion { PeriodoId = t4.Id, ClienteId = sl.Id, UsuarioId = nuria.Id },
            new Asignacion { PeriodoId = t4.Id, ClienteId = angel.Id, UsuarioId = null });
        db.SaveChanges();
        return (e, nuria, javi);
    }

    private static string[] Nombres(IEnumerable<ClienteResumen> lista) => lista.Select(c => c.Nombre).ToArray();

    [Fact]
    public async Task Sin_filtros_lista_todos_ordenados_por_nombre_sin_acentos()
    {
        var (e, _, _) = await Preparar();
        using var _e = e;

        var lista = await e.Servicio.ListarAsync(new FiltroClientes());

        Assert.Equal(new[] { "Ana López", "Ángel Núñez", "Construcciones SL" }, Nombres(lista));
    }

    [Theory]
    [InlineData("lopez", "Ana López")]
    [InlineData("LÓPEZ", "Ana López")]
    [InlineData("angel nuñez", "Ángel Núñez")]
    [InlineData("x1234", "Ángel Núñez")]
    [InlineData("construcciones", "Construcciones SL")]
    public async Task Busca_por_nombre_o_NIF_sin_mayusculas_ni_acentos(string texto, string esperado)
    {
        var (e, _, _) = await Preparar();
        using var _e = e;

        var lista = await e.Servicio.ListarAsync(new FiltroClientes(texto));

        Assert.Equal(new[] { esperado }, Nombres(lista));
    }

    [Fact]
    public void La_n_con_tilde_no_se_confunde_con_la_n()
    {
        Assert.Equal("nunez", TextoBusqueda.Normalizar("Núnez"));
        Assert.Equal("ñu", TextoBusqueda.Normalizar("Ñu"));
        Assert.NotEqual(TextoBusqueda.Normalizar("Muñoz"), TextoBusqueda.Normalizar("Munoz"));
    }

    [Fact]
    public async Task Filtra_por_estado()
    {
        var (e, _, _) = await Preparar();
        using var _e = e;

        Assert.Equal(new[] { "Ana López", "Construcciones SL" },
            Nombres(await e.Servicio.ListarAsync(new FiltroClientes(Estado: FiltroEstadoCliente.Activos))));
        Assert.Equal(new[] { "Ángel Núñez" },
            Nombres(await e.Servicio.ListarAsync(new FiltroClientes(Estado: FiltroEstadoCliente.DeBaja))));
    }

    [Fact]
    public async Task El_empleado_habitual_es_el_del_ultimo_periodo_asignado()
    {
        var (e, _, _) = await Preparar();
        using var _e = e;

        var lista = await e.Servicio.ListarAsync(new FiltroClientes());

        Assert.Equal("Javi", lista.Single(c => c.Nombre == "Ana López").EmpleadoHabitual);
        Assert.Equal("Nuria", lista.Single(c => c.Nombre == "Construcciones SL").EmpleadoHabitual);
        Assert.Equal(ClienteResumen.SinAsignar, lista.Single(c => c.Nombre == "Ángel Núñez").EmpleadoHabitual);
    }

    [Fact]
    public async Task Filtra_por_empleado_habitual_y_por_sin_asignar()
    {
        var (e, nuria, javi) = await Preparar();
        using var _e = e;

        Assert.Equal(new[] { "Construcciones SL" }, Nombres(await e.Servicio.ListarAsync(new FiltroClientes(EmpleadoId: nuria.Id))));
        Assert.Equal(new[] { "Ana López" }, Nombres(await e.Servicio.ListarAsync(new FiltroClientes(EmpleadoId: javi.Id))));
        Assert.Equal(new[] { "Ángel Núñez" }, Nombres(await e.Servicio.ListarAsync(new FiltroClientes(SoloSinAsignar: true))));
    }

    [Fact]
    public async Task Combina_los_filtros()
    {
        var (e, nuria, _) = await Preparar();
        using var _e = e;

        var lista = await e.Servicio.ListarAsync(new FiltroClientes("sl", TipoCliente.Sociedad, FiltroEstadoCliente.Activos, nuria.Id));
        var ninguno = await e.Servicio.ListarAsync(new FiltroClientes(Tipo: TipoCliente.Fisica, EmpleadoId: nuria.Id));

        Assert.Equal(new[] { "Construcciones SL" }, Nombres(lista));
        Assert.Empty(ninguno);
    }

    [Fact]
    public async Task ViewModel_carga_los_empleados_y_filtra()
    {
        var (e, nuria, _) = await Preparar();
        using var _e = e;
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));

        await vm.AlEntrarAsync();
        Assert.Equal("3 clientes", vm.Recuento);
        Assert.Contains(vm.FiltrosEmpleado, o => o.Texto == "Nuria");
        Assert.False(vm.HayFiltros);

        vm.FiltroEmpleado = vm.FiltrosEmpleado.Single(o => o.Id == nuria.Id);
        await vm.CargarCommand.ExecuteAsync(null);
        Assert.Equal("1 cliente", vm.Recuento);
        Assert.True(vm.HayFiltros);

        vm.LimpiarFiltrosCommand.Execute(null);
        await vm.CargarCommand.ExecuteAsync(null);
        Assert.Equal("3 clientes", vm.Recuento);
        Assert.False(vm.HayFiltros);
    }

    [Fact]
    public async Task ViewModel_avisa_si_no_hay_resultados()
    {
        var (e, _, _) = await Preparar();
        using var _e = e;
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();

        vm.FiltroEstado = ClientesViewModel.FiltrosEstado.Single(o => o.Valor == FiltroEstadoCliente.DeBaja);
        vm.FiltroSeleccionado = ClientesViewModel.FiltrosTipo.Single(o => o.Valor == TipoCliente.Sociedad);
        await vm.CargarCommand.ExecuteAsync(null);

        Assert.True(vm.ListaVacia);
    }
}
