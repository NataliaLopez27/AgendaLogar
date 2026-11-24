using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.Tests;

public class CU12_EditarClienteTests
{
    [Fact]
    public async Task Modifica_nombre_NIF_y_notas()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica("Ana", "12345678Z");

        var editado = await e.Servicio.EditarAsync(c.Id, new DatosCliente("Ana López García", "x1234567l", TipoCliente.Fisica, "  arrendamiento "));

        Assert.Equal("Ana López García", editado.Nombre);
        Assert.Equal("X1234567L", editado.Nif);
        var datos = await e.Servicio.ObtenerDatosAsync(c.Id);
        Assert.Equal("arrendamiento", datos.Notas);
    }

    [Fact]
    public async Task Aplica_las_mismas_validaciones_que_el_alta()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => e.Servicio.EditarAsync(c.Id, new DatosCliente("Ana", "12345678A", TipoCliente.Fisica, null)));

        Assert.Equal("El NIF/CIF 12345678A no es válido.", ex.Message);
    }

    [Fact]
    public async Task No_cambia_el_tipo_si_lleva_modelos_que_no_aplican()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(modelos: new[] { "130", "303", "100" });

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => e.Servicio.EditarAsync(c.Id, new DatosCliente("Ana", "12345678Z", TipoCliente.Sociedad, null)));

        Assert.Contains("(100, 130)", ex.Message);
    }

    [Fact]
    public async Task Cambia_el_tipo_si_sus_modelos_aplican_a_los_dos()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(modelos: new[] { "303", "111" });

        var editado = await e.Servicio.EditarAsync(c.Id, new DatosCliente("Ana SL", "B12345674", TipoCliente.Sociedad, null));

        Assert.Equal(TipoCliente.Sociedad, editado.Tipo);
    }

    [Fact]
    public async Task No_toca_las_fechas_de_alta()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(alta: "2025-03-15");

        var editado = await e.Servicio.EditarAsync(c.Id, new DatosCliente("Otro nombre", "12345678Z", TipoCliente.Fisica, null));

        Assert.Equal(EscenarioClientes.F("2025-03-15"), editado.FechaAlta);
    }

    [Fact]
    public async Task ViewModel_edita_el_cliente_seleccionado()
    {
        using var e = new EscenarioClientes();
        await e.CrearFisica("Ana", "12345678Z");
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();
        vm.Seleccionado = vm.Clientes.Single();

        await vm.EditarCommand.ExecuteAsync(null);
        Assert.Equal(ModoPanelCliente.Edicion, vm.Modo);
        Assert.Equal("Ana", vm.Nombre);
        vm.Nombre = "Ana López";
        await vm.GuardarPanelCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorPanel);
        Assert.Equal("Ana López", vm.Clientes.Single().Nombre);
    }

    [Fact]
    public async Task ViewModel_avisa_si_el_NIF_nuevo_es_de_otro_cliente_pero_no_de_si_mismo()
    {
        using var e = new EscenarioClientes();
        await e.CrearFisica("Ana", "12345678Z");
        await e.CrearFisica("Berta", "X1234567L");
        var dialogos = new DialogosFalsos(false);
        var vm = new ClientesViewModel(e.Servicio, dialogos);
        await vm.AlEntrarAsync();
        vm.Seleccionado = vm.Clientes.Single(c => c.Nombre == "Berta");
        await vm.EditarCommand.ExecuteAsync(null);

        vm.Notas = "solo cambio las notas";
        await vm.GuardarPanelCommand.ExecuteAsync(null);
        Assert.Equal(0, dialogos.Veces);

        await vm.EditarCommand.ExecuteAsync(null);
        vm.Nif = "12345678Z";
        await vm.GuardarPanelCommand.ExecuteAsync(null);
        Assert.Equal(1, dialogos.Veces);
        Assert.Equal("X1234567L", vm.Clientes.Single(c => c.Nombre == "Berta").Nif);
    }

    [Fact]
    public void ViewModel_no_se_puede_editar_sin_seleccion()
    {
        using var e = new EscenarioClientes();
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));
        Assert.False(vm.EditarCommand.CanExecute(null));
    }
}
