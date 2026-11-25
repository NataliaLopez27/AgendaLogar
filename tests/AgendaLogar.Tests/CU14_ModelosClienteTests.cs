using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.ViewModels;
using static AgendaLogar.Tests.EscenarioClientes;

namespace AgendaLogar.Tests;

public class CU14_ModelosClienteTests
{
    [Fact]
    public async Task Anade_un_modelo_con_su_fecha_de_alta()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(modelos: "303");

        var t = await e.Servicio.AnadirModeloAsync(c.Id, "115", F("2026-07-17"), "arrendamiento");

        Assert.True(t.Vigente);
        Assert.Equal("arrendamiento", t.Notas);
        var modelos = await e.Servicio.ModelosDeClienteAsync(c.Id);
        Assert.Equal(new[] { "115", "303" }, modelos.Select(m => m.Codigo).OrderBy(x => x));
        Assert.Equal("115, 303", (await e.Servicio.ListarAsync()).Single().Modelos);
    }

    [Fact]
    public async Task No_anade_un_modelo_que_no_aplica_al_tipo()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.AnadirModeloAsync(c.Id, "202", F("2026-01-01")));

        Assert.Equal("El modelo 202 no se aplica a personas físicas.", ex.Message);
    }

    [Fact]
    public async Task No_anade_un_modelo_que_ya_lleva()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica("Ana", modelos: "303");

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.AnadirModeloAsync(c.Id, "303", F("2026-05-01")));

        Assert.Equal("Ana ya lleva el modelo 303.", ex.Message);
    }

    [Fact]
    public async Task Baja_IVA_31_3_25_y_el_303_deja_de_estar_vigente_en_el_2T()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(alta: "2024-01-01", modelos: "303");
        var tramo = (await e.Servicio.ModelosDeClienteAsync(c.Id)).Single();

        await e.Servicio.DarDeBajaModeloAsync(tramo.Id, F("2025-03-31"));

        var despues = (await e.Servicio.ModelosDeClienteAsync(c.Id)).Single();
        Assert.Equal("Baja 31/03/2025", despues.EstadoTexto);
        Assert.Equal("", (await e.Servicio.ListarAsync()).Single().Modelos);
        using var db = e.Bd.CreateDbContext();
        var tramos = db.ClienteModelos.Where(m => m.ClienteId == c.Id).ToList();
        Assert.True(Vigencia.ModeloVigente(tramos, "303", new Periodo { Anio = 2025, Trimestre = 1 }));
        Assert.False(Vigencia.ModeloVigente(tramos, "303", new Periodo { Anio = 2025, Trimestre = 2 }));
    }

    [Fact]
    public async Task La_baja_del_modelo_no_puede_ser_anterior_a_su_alta()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(alta: "2026-07-01", modelos: "303");
        var tramo = (await e.Servicio.ModelosDeClienteAsync(c.Id)).Single();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.DarDeBajaModeloAsync(tramo.Id, F("2026-06-30")));

        Assert.Contains("no puede ser anterior", ex.Message);
    }

    [Fact]
    public async Task Se_puede_volver_a_dar_de_alta_despues_de_la_baja_sin_solapes()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(alta: "2024-01-01", modelos: "303");
        var tramo = (await e.Servicio.ModelosDeClienteAsync(c.Id)).Single();
        await e.Servicio.DarDeBajaModeloAsync(tramo.Id, F("2025-03-31"));

        var solapa = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.AnadirModeloAsync(c.Id, "303", F("2025-03-31")));
        await e.Servicio.AnadirModeloAsync(c.Id, "303", F("2025-10-01"));

        Assert.Contains("posterior a su última baja (31/03/2025)", solapa.Message);
        var tramos = await e.Servicio.ModelosDeClienteAsync(c.Id);
        Assert.Equal(2, tramos.Count);
        Assert.True(tramos[0].Vigente);   // el más reciente primero
        Assert.False(tramos[1].Vigente);
    }

    [Fact]
    public async Task Guarda_las_notas_del_modelo()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(modelos: "111");
        var tramo = (await e.Servicio.ModelosDeClienteAsync(c.Id)).Single();

        await e.Servicio.CambiarNotasModeloAsync(tramo.Id, "  OJO CAMBIAR NUMERO CUENTA ");

        Assert.Equal("OJO CAMBIAR NUMERO CUENTA", (await e.Servicio.ModelosDeClienteAsync(c.Id)).Single().Notas);
    }

    [Fact]
    public async Task ViewModel_gestiona_los_modelos_sin_cerrar_el_panel()
    {
        using var e = new EscenarioClientes();
        await e.CrearFisica("Ana", alta: "2024-01-01", modelos: "303");
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();
        vm.Seleccionado = vm.Clientes.Single();

        await vm.GestionarModelosCommand.ExecuteAsync(null);
        Assert.True(vm.EsModelos);
        Assert.False(vm.MostrarGuardar);
        Assert.Single(vm.ModelosCliente);
        Assert.DoesNotContain(vm.ModelosParaAnadir, m => m.Codigo == "200");

        vm.ModeloAnadir = vm.ModelosParaAnadir.Single(m => m.Codigo == "115");
        vm.FechaAltaModelo = new DateTime(2026, 7, 17);
        await vm.AnadirModeloCommand.ExecuteAsync(null);
        Assert.Null(vm.ErrorPanel);
        Assert.Equal(2, vm.ModelosCliente.Count);
        Assert.Equal("115", vm.TramoSeleccionado?.Codigo);

        vm.TramoSeleccionado = vm.ModelosCliente.Single(t => t.Codigo == "303");
        vm.FechaBajaModelo = new DateTime(2025, 3, 31);
        await vm.DarDeBajaModeloCommand.ExecuteAsync(null);

        Assert.True(vm.EsModelos);
        Assert.False(vm.ModelosCliente.Single(t => t.Codigo == "303").Vigente);
        Assert.Equal("115", vm.Clientes.Single().Modelos);
        Assert.False(vm.DarDeBajaModeloCommand.CanExecute(null));
    }

    [Fact]
    public async Task ViewModel_muestra_el_error_al_anadir_un_modelo_repetido()
    {
        using var e = new EscenarioClientes();
        await e.CrearFisica("Ana", modelos: "303");
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();
        vm.Seleccionado = vm.Clientes.Single();
        await vm.GestionarModelosCommand.ExecuteAsync(null);

        vm.ModeloAnadir = vm.ModelosParaAnadir.Single(m => m.Codigo == "303");
        await vm.AnadirModeloCommand.ExecuteAsync(null);

        Assert.Equal("Ana ya lleva el modelo 303.", vm.ErrorPanel);
    }
}
