using AgendaLogar.Core.Dominio;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.Tests;

public class Hito3_PeriodosViewModelTests
{
    [Fact]
    public async Task Propone_el_trimestre_en_curso_y_si_existe_el_siguiente()
    {
        using var e = new EscenarioPeriodos();
        var hoy = Periodo.TrimestreDe(DateOnly.FromDateTime(DateTime.Today));
        var vm = new PeriodosViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();

        vm.AbrirNuevoCommand.Execute(null);
        Assert.Equal(hoy.Nombre, vm.NombreNuevo);

        await vm.CrearPeriodoCommand.ExecuteAsync(null);
        vm.AbrirNuevoCommand.Execute(null);
        Assert.NotEqual(hoy.Nombre, vm.NombreNuevo);
    }

    [Fact]
    public async Task Crea_el_periodo_lo_selecciona_y_muestra_el_resumen()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        var vm = new PeriodosViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();
        Assert.False(vm.HayPeriodos);

        vm.AbrirNuevoCommand.Execute(null);
        vm.NuevoAnio = 2026;
        vm.NuevoTrimestre = 4;
        await vm.CrearPeriodoCommand.ExecuteAsync(null);

        Assert.Null(vm.Error);
        Assert.False(vm.MostrarNuevo);
        Assert.Equal("4T 2026", vm.PeriodoSeleccionado?.Nombre);
        Assert.StartsWith("Se ha creado el 4T 2026 con 1 clientes", vm.Aviso);
        Assert.Single(vm.Filas);
    }

    [Fact]
    public async Task Muestra_el_error_si_el_periodo_ya_existe()
    {
        using var e = new EscenarioPeriodos();
        await e.Servicio.CrearAsync(2026, 4, true);
        var vm = new PeriodosViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();

        vm.AbrirNuevoCommand.Execute(null);
        vm.NuevoAnio = 2026;
        vm.NuevoTrimestre = 4;
        await vm.CrearPeriodoCommand.ExecuteAsync(null);

        Assert.Equal("El periodo 4T 2026 ya existe.", vm.Error);
        Assert.True(vm.MostrarNuevo);
    }

    [Fact]
    public async Task Asigna_los_seleccionados_al_empleado_elegido()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        await e.Clientes.CrearFisica("Berta", "X1234567L", "2020-01-01", "303");
        await e.Servicio.CrearAsync(2026, 4, true);
        var vm = new PeriodosViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();

        Assert.False(vm.AsignarSeleccionadosCommand.CanExecute(null));
        vm.TodosSeleccionados = true;
        Assert.Equal("2 seleccionados", vm.TextoSeleccion);
        vm.EmpleadoDestino = vm.Empleados.Single(u => u.Nombre == "Nuria");
        Assert.True(vm.AsignarSeleccionadosCommand.CanExecute(null));
        await vm.AsignarSeleccionadosCommand.ExecuteAsync(null);

        Assert.Equal("2 cliente(s) asignados a Nuria.", vm.Aviso);
        Assert.All(vm.Filas, f => Assert.Equal("Nuria", f.Fila.Empleado));
        Assert.Equal("Todos asignados", vm.PeriodoSeleccionado?.SinAsignarTexto);
        Assert.Equal(0, vm.Seleccionadas);
    }

    [Fact]
    public async Task Filtra_los_sin_asignar()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        await e.Clientes.CrearFisica("Berta", "X1234567L", "2020-01-01", "303");
        var p = await e.Servicio.CrearAsync(2026, 4, true);
        var ana = (await e.Filas(p.PeriodoId)).Single(f => f.Cliente == "Ana");
        await e.Servicio.AsignarAsync(p.PeriodoId, new[] { ana.Id }, e.Javi.Id);
        var vm = new PeriodosViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();

        vm.FiltroEmpleado = OpcionEmpleado.NingunoAsignado;
        await vm.CargarFilasAsync();

        Assert.Equal("Berta", vm.Filas.Single().Fila.Cliente);
    }

    [Fact]
    public async Task Cerrar_pide_confirmacion_y_luego_bloquea_la_asignacion()
    {
        using var e = new EscenarioPeriodos();
        await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        await e.Servicio.CrearAsync(2026, 4, true);
        var noConfirma = new PeriodosViewModel(e.Servicio, new DialogosFalsos(false));
        await noConfirma.AlEntrarAsync();
        await noConfirma.CerrarPeriodoCommand.ExecuteAsync(null);
        Assert.True(noConfirma.PeriodoAbierto);

        var vm = new PeriodosViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();
        await vm.CerrarPeriodoCommand.ExecuteAsync(null);

        Assert.True(vm.PeriodoCerrado);
        vm.TodosSeleccionados = true;
        vm.EmpleadoDestino = vm.Empleados.First();
        Assert.False(vm.AsignarSeleccionadosCommand.CanExecute(null));
        Assert.True(vm.ReabrirPeriodoCommand.CanExecute(null));

        await vm.ReabrirPeriodoCommand.ExecuteAsync(null);
        Assert.True(vm.PeriodoAbierto);
    }
}

public class RN04_ClientesEnPeriodosAbiertosTests
{
    [Fact]
    public async Task Al_dar_de_alta_propone_anadirlo_a_los_periodos_abiertos()
    {
        using var e = new EscenarioPeriodos();
        var p = await e.Servicio.CrearAsync(2026, 4, true);
        var dialogos = new DialogosFalsos(true);
        var vm = new ClientesViewModel(e.ServicioClientes, dialogos, e.Servicio);
        await vm.AlEntrarAsync();

        await vm.NuevoCommand.ExecuteAsync(null);
        vm.Nombre = "Nuevo";
        vm.Nif = "12345678Z";
        vm.FechaAlta = new DateTime(2026, 11, 2);
        await vm.GuardarPanelCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogos.Veces);
        Assert.Contains("Añadido a: 4T 2026.", vm.Aviso);
        Assert.Single(await e.Filas(p.PeriodoId));
    }

    [Fact]
    public async Task Si_no_se_confirma_no_lo_anade()
    {
        using var e = new EscenarioPeriodos();
        var p = await e.Servicio.CrearAsync(2026, 4, true);
        var vm = new ClientesViewModel(e.ServicioClientes, new DialogosFalsos(false), e.Servicio);
        await vm.AlEntrarAsync();

        await vm.NuevoCommand.ExecuteAsync(null);
        vm.Nombre = "Nuevo";
        vm.Nif = "12345678Z";
        vm.FechaAlta = new DateTime(2026, 11, 2);
        await vm.GuardarPanelCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorPanel);
        Assert.Empty(await e.Filas(p.PeriodoId));
    }

    [Fact]
    public async Task Al_reactivar_tambien_lo_propone()
    {
        using var e = new EscenarioPeriodos();
        var ana = await e.Clientes.CrearFisica("Ana", "12345678Z", "2020-01-01", "303");
        await e.ServicioClientes.DarDeBajaAsync(ana.Id, EscenarioClientes.F("2026-03-31"));
        var p = await e.Servicio.CrearAsync(2026, 4, true);
        Assert.Empty(await e.Filas(p.PeriodoId));
        var vm = new ClientesViewModel(e.ServicioClientes, new DialogosFalsos(true), e.Servicio);
        await vm.AlEntrarAsync();
        vm.Seleccionado = vm.Clientes.Single();

        await vm.ReactivarCommand.ExecuteAsync(null);
        vm.FechaOperacion = new DateTime(2026, 11, 2);
        await vm.GuardarPanelCommand.ExecuteAsync(null);

        Assert.Single(await e.Filas(p.PeriodoId));
    }
}
