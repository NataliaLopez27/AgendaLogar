using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.ViewModels;
using static AgendaLogar.Tests.EscenarioClientes;

namespace AgendaLogar.Tests;

/// <summary>RN-01 / RN-02: regla de vigencia con los casos límite.</summary>
public class VigenciaTests
{
    private static Periodo T(int anio, byte t) => new() { Anio = anio, Trimestre = t };
    private static Periodo Anual(int anio) => new() { Anio = anio };

    [Theory]
    // Ejemplo de la sección 4.3.1: baja el 24-11-2026
    [InlineData("2020-01-01", "2026-11-24", 2026, 4, true)]
    [InlineData("2020-01-01", "2026-11-24", 2027, 1, false)]
    // Baja el primer día del trimestre: ha estado de alta ese día, así que entra
    [InlineData("2020-01-01", "2026-10-01", 2026, 4, true)]
    // Baja el último día del trimestre anterior: no entra
    [InlineData("2020-01-01", "2026-09-30", 2026, 4, false)]
    // Alta el último día del trimestre: entra
    [InlineData("2026-12-31", null, 2026, 4, true)]
    // Alta el primer día del trimestre siguiente: no entra en el anterior
    [InlineData("2027-01-01", null, 2026, 4, false)]
    // Sin baja: sigue entrando
    [InlineData("2020-01-01", null, 2030, 2, true)]
    public void Un_tramo_entra_en_el_trimestre_si_estuvo_de_alta_algun_dia(string alta, string? baja, int anio, byte trimestre, bool esperado)
    {
        var bajaFecha = baja is null ? (DateOnly?)null : F(baja);
        Assert.Equal(esperado, Vigencia.EsVigente(F(alta), bajaFecha, T(anio, trimestre)));
    }

    [Fact]
    public void Ejemplo_del_documento_en_los_periodos_anuales()
    {
        Assert.True(Vigencia.EsVigente(F("2020-01-01"), F("2026-11-24"), Anual(2026)));
        Assert.False(Vigencia.EsVigente(F("2020-01-01"), F("2026-11-24"), Anual(2027)));
    }

    [Fact]
    public void Con_baja_y_reactivacion_cuenta_cualquiera_de_sus_tramos()
    {
        var altas = new[]
        {
            new AltaCliente { FechaAlta = F("2024-01-01"), FechaBaja = F("2025-03-31") },
            new AltaCliente { FechaAlta = F("2025-10-15") }
        };

        Assert.True(Vigencia.ClienteVigente(altas, T(2025, 1)));
        Assert.False(Vigencia.ClienteVigente(altas, T(2025, 2)));
        Assert.False(Vigencia.ClienteVigente(altas, T(2025, 3)));
        Assert.True(Vigencia.ClienteVigente(altas, T(2025, 4)));
    }

    [Fact]
    public void RN02_baja_IVA_31_3_25_el_303_sale_en_el_1T_y_no_en_el_2T()
    {
        var modelos = new[] { new ClienteModelo { ModeloCodigo = "303", FechaAlta = F("2020-01-01"), FechaBaja = F("2025-03-31") } };

        Assert.True(Vigencia.ModeloVigente(modelos, "303", T(2025, 1)));
        Assert.False(Vigencia.ModeloVigente(modelos, "303", T(2025, 2)));
    }

    [Theory]
    [InlineData("2026-01-01", "1T 2026")]
    [InlineData("2026-03-31", "1T 2026")]
    [InlineData("2026-11-24", "4T 2026")]
    public void TrimestreDe_devuelve_el_trimestre_que_contiene_la_fecha(string fecha, string nombre) =>
        Assert.Equal(nombre, Periodo.TrimestreDe(F(fecha)).Nombre);
}

public class CU13_BajaReactivacionTests
{
    [Fact]
    public async Task La_baja_cierra_el_tramo_y_no_borra_el_cliente()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(alta: "2024-01-01");

        var baja = await e.Servicio.DarDeBajaAsync(c.Id, F("2026-11-24"));

        Assert.False(baja.Activo);
        Assert.Equal("Baja 24/11/2026", baja.EstadoTexto);
        Assert.Single(await e.Servicio.ListarAsync());
        Assert.Empty(await e.Servicio.ListarAsync(incluirBajas: false));
    }

    [Fact]
    public async Task La_baja_no_puede_ser_anterior_al_alta()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(alta: "2026-07-01");

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.DarDeBajaAsync(c.Id, F("2026-06-30")));

        Assert.Contains("no puede ser anterior", ex.Message);
    }

    [Fact]
    public async Task La_baja_puede_ser_el_mismo_dia_del_alta()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(alta: "2026-07-01");

        var baja = await e.Servicio.DarDeBajaAsync(c.Id, F("2026-07-01"));

        Assert.False(baja.Activo);
    }

    [Fact]
    public async Task No_se_puede_dar_de_baja_dos_veces()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica("Ana");
        await e.Servicio.DarDeBajaAsync(c.Id, F("2026-05-01"));

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.DarDeBajaAsync(c.Id, F("2026-06-01")));

        Assert.Equal("Ana ya está dado de baja.", ex.Message);
    }

    [Fact]
    public async Task Reactivar_abre_un_tramo_nuevo_y_conserva_el_historial()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(alta: "2024-01-01");
        await e.Servicio.DarDeBajaAsync(c.Id, F("2025-03-31"));

        var r = await e.Servicio.ReactivarAsync(c.Id, F("2025-10-15"));
        var historial = await e.Servicio.HistorialAltasAsync(c.Id);

        Assert.True(r.Activo);
        Assert.Equal(F("2025-10-15"), r.FechaAlta);
        Assert.Equal(2, historial.Count);
        Assert.Equal(new TramoAlta(F("2025-10-15"), null), historial[0]);
        Assert.Equal(new TramoAlta(F("2024-01-01"), F("2025-03-31")), historial[1]);
    }

    [Theory]
    [InlineData("2025-03-31")]
    [InlineData("2025-01-01")]
    public async Task La_nueva_alta_tiene_que_ser_posterior_a_la_ultima_baja(string nuevaAlta)
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica(alta: "2024-01-01");
        await e.Servicio.DarDeBajaAsync(c.Id, F("2025-03-31"));

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.ReactivarAsync(c.Id, F(nuevaAlta)));

        Assert.Contains("posterior a la última baja (31/03/2025)", ex.Message);
    }

    [Fact]
    public async Task No_se_puede_reactivar_a_un_cliente_de_alta()
    {
        using var e = new EscenarioClientes();
        var c = await e.CrearFisica("Ana");

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.ReactivarAsync(c.Id, F("2026-12-01")));

        Assert.Equal("Ana ya está de alta.", ex.Message);
    }

    [Fact]
    public async Task ViewModel_da_de_baja_y_reactiva()
    {
        using var e = new EscenarioClientes();
        await e.CrearFisica("Ana", alta: "2024-01-01");
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();
        vm.Seleccionado = vm.Clientes.Single();
        Assert.True(vm.DarDeBajaCommand.CanExecute(null));
        Assert.False(vm.ReactivarCommand.CanExecute(null));

        await vm.DarDeBajaCommand.ExecuteAsync(null);
        Assert.Equal(ModoPanelCliente.Baja, vm.Modo);
        Assert.Single(vm.Historial);
        vm.FechaOperacion = new DateTime(2026, 11, 24);
        Assert.Contains("4T 2026", vm.Explicacion);
        await vm.GuardarPanelCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorPanel);
        Assert.Equal("Baja 24/11/2026", vm.Seleccionado?.EstadoTexto);
        Assert.True(vm.ReactivarCommand.CanExecute(null));
        Assert.False(vm.DarDeBajaCommand.CanExecute(null));

        await vm.ReactivarCommand.ExecuteAsync(null);
        vm.FechaOperacion = new DateTime(2027, 2, 1);
        Assert.Contains("1T 2027", vm.Explicacion);
        await vm.GuardarPanelCommand.ExecuteAsync(null);

        Assert.True(vm.Seleccionado?.Activo);
    }

    [Fact]
    public async Task ViewModel_muestra_el_error_si_la_fecha_no_vale()
    {
        using var e = new EscenarioClientes();
        await e.CrearFisica("Ana", alta: "2026-07-01");
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();
        vm.Seleccionado = vm.Clientes.Single();
        await vm.DarDeBajaCommand.ExecuteAsync(null);

        vm.FechaOperacion = new DateTime(2026, 1, 1);
        await vm.GuardarPanelCommand.ExecuteAsync(null);

        Assert.Contains("no puede ser anterior", vm.ErrorPanel);
        Assert.True(vm.PanelVisible);
    }
}
