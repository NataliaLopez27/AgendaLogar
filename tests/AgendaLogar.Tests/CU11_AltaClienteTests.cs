using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.Navegacion;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.Tests;

/// <summary>Escenario con un administrador conectado y el servicio de clientes.</summary>
public sealed class EscenarioClientes : IDisposable
{
    public BaseDatosPrueba Bd { get; } = new();
    public Usuario Admin { get; }
    public ServicioClientes Servicio { get; }

    public EscenarioClientes()
    {
        Admin = Datos.CrearUsuario(Bd, "jefe", "secreta123", Rol.Admin, nombre: "Jefe");
        Servicio = new ServicioClientes(Bd, new ControlAcceso(Datos.SesionDe(Admin)));
    }

    public static DateOnly F(string fecha) => DateOnly.Parse(fecha);

    public Task<ClienteResumen> CrearFisica(string nombre = "Ana López", string nif = "12345678Z", string alta = "2026-01-01",
        params string[] modelos) =>
        Servicio.CrearAsync(new DatosCliente(nombre, nif, TipoCliente.Fisica, null), F(alta),
            modelos.Select(m => new ModeloAlta(m, F(alta))));

    public void Dispose() => Bd.Dispose();
}

public sealed class DialogosFalsos(bool respuesta) : IDialogos
{
    public int Veces { get; private set; }
    public bool Confirmar(string titulo, string mensaje)
    {
        Veces++;
        return respuesta;
    }
}

public class ValidadorNifTests
{
    [Theory]
    [InlineData("12345678Z")]
    [InlineData("12345678-z")]
    [InlineData(" 12.345.678 Z ")]
    [InlineData("X1234567L")]
    [InlineData("Y7654321G")]
    [InlineData("Z1111111D")]
    [InlineData("B12345674")]
    [InlineData("A58818501")]
    [InlineData("P2800000H")]
    [InlineData("Q2876031B")]
    public void Acepta_DNI_NIE_y_CIF_validos(string nif) => Assert.True(ValidadorNif.EsValido(nif));

    [Theory]
    [InlineData("")]
    [InlineData("12345678A")]   // letra de control incorrecta
    [InlineData("1234567Z")]    // corto
    [InlineData("X1234567A")]
    [InlineData("B12345675")]   // dígito de control incorrecto
    [InlineData("B1234567D")]   // la B exige dígito
    [InlineData("P28000008")]   // la P exige letra
    [InlineData("I12345674")]   // letra inicial no válida
    public void Rechaza_NIF_no_validos(string nif) => Assert.False(ValidadorNif.EsValido(nif));
}

public class CU11_AltaClienteTests
{
    [Fact]
    public async Task Da_de_alta_un_cliente_con_sus_modelos()
    {
        using var e = new EscenarioClientes();

        var c = await e.CrearFisica("  Ana López ", "12345678-z", "2026-07-17", "303", "130", "390");

        Assert.Equal("Ana López", c.Nombre);
        Assert.Equal("12345678Z", c.Nif);
        Assert.True(c.Activo);
        Assert.Equal(EscenarioClientes.F("2026-07-17"), c.FechaAlta);
        Assert.Equal("130, 303, 390", c.Modelos);
        using var db = e.Bd.CreateDbContext();
        Assert.Single(db.AltasCliente.Where(a => a.ClienteId == c.Id && a.FechaBaja == null));
        Assert.Equal(3, db.ClienteModelos.Count(m => m.ClienteId == c.Id));
    }

    [Theory]
    [InlineData("", "12345678Z", "Indica el nombre o la razón social.")]
    [InlineData("Ana", "", "Indica el NIF/CIF.")]
    [InlineData("Ana", "12345678A", "El NIF/CIF 12345678A no es válido.")]
    public async Task Valida_los_datos(string nombre, string nif, string mensaje)
    {
        using var e = new EscenarioClientes();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.CrearFisica(nombre, nif));

        Assert.Equal(mensaje, ex.Message);
    }

    [Fact]
    public async Task No_permite_modelos_que_no_aplican_al_tipo()
    {
        using var e = new EscenarioClientes();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.CrearFisica(modelos: "200"));

        Assert.Equal("El modelo 200 no se aplica a personas físicas.", ex.Message);
    }

    [Fact]
    public async Task La_fecha_del_modelo_no_puede_ser_anterior_a_la_del_cliente()
    {
        using var e = new EscenarioClientes();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.CrearAsync(
            new DatosCliente("Ana", "12345678Z", TipoCliente.Fisica, null), EscenarioClientes.F("2026-07-01"),
            new[] { new ModeloAlta("303", EscenarioClientes.F("2026-06-30")) }));

        Assert.Contains("no puede ser anterior", ex.Message);
    }

    [Fact]
    public async Task El_NIF_duplicado_se_detecta_pero_no_bloquea()
    {
        using var e = new EscenarioClientes();
        await e.CrearFisica("Ana", "12345678Z");

        var otro = await e.Servicio.BuscarNifDuplicadoAsync("12345678-z");
        await e.CrearFisica("Ana (duplicada)", "12345678Z");

        Assert.Equal("Ana", otro?.Nombre);
        Assert.Equal(2, (await e.Servicio.ListarAsync()).Count);
    }

    [Fact]
    public async Task Solo_ofrece_los_modelos_que_aplican_a_cada_tipo()
    {
        using var e = new EscenarioClientes();

        var fisicas = (await e.Servicio.ModelosParaAsync(TipoCliente.Fisica)).Select(m => m.Codigo).ToList();
        var sociedades = (await e.Servicio.ModelosParaAsync(TipoCliente.Sociedad)).Select(m => m.Codigo).ToList();

        Assert.Contains("130", fisicas);
        Assert.DoesNotContain("200", fisicas);
        Assert.Contains("200", sociedades);
        Assert.DoesNotContain("100", sociedades);
    }

    [Fact]
    public async Task El_listado_busca_por_nombre_o_NIF_y_filtra_por_tipo()
    {
        using var e = new EscenarioClientes();
        await e.CrearFisica("Ana López", "12345678Z");
        await e.Servicio.CrearAsync(new DatosCliente("Construcciones SL", "B12345674", TipoCliente.Sociedad, null),
            EscenarioClientes.F("2026-01-01"), Array.Empty<ModeloAlta>());

        Assert.Single(await e.Servicio.ListarAsync("lópez"));
        Assert.Single(await e.Servicio.ListarAsync("b1234"));
        Assert.Equal("Construcciones SL", (await e.Servicio.ListarAsync(tipo: TipoCliente.Sociedad)).Single().Nombre);
    }

    [Fact]
    public async Task Un_empleado_no_puede_gestionar_clientes()
    {
        using var bd = new BaseDatosPrueba();
        var empleado = Datos.CrearUsuario(bd, "nuria", "secreta123");
        var servicio = new ServicioClientes(bd, new ControlAcceso(Datos.SesionDe(empleado)));

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => servicio.ListarAsync());
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => servicio.CrearAsync(
            new DatosCliente("Ana", "12345678Z", TipoCliente.Fisica, null), EscenarioClientes.F("2026-01-01"), Array.Empty<ModeloAlta>()));
    }

    [Fact]
    public async Task ViewModel_alta_completa_con_modelos()
    {
        using var e = new EscenarioClientes();
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();

        await vm.NuevoCommand.ExecuteAsync(null);
        vm.Nombre = "Ana López";
        vm.Nif = "12345678Z";
        vm.FechaAlta = new DateTime(2026, 7, 17);
        vm.ModelosDisponibles.Single(m => m.Codigo == "303").Marcado = true;
        vm.ModelosDisponibles.Single(m => m.Codigo == "111").Marcado = true;
        await vm.GuardarPanelCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorPanel);
        Assert.False(vm.PanelVisible);
        var c = Assert.Single(vm.Clientes);
        Assert.Equal("111, 303", c.Modelos);
        Assert.Equal(c.Id, vm.Seleccionado?.Id);
    }

    [Fact]
    public async Task ViewModel_al_cambiar_a_sociedad_ofrece_sus_modelos()
    {
        using var e = new EscenarioClientes();
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.NuevoCommand.ExecuteAsync(null);
        Assert.Contains(vm.ModelosDisponibles, m => m.Codigo == "130");

        vm.Tipo = TipoCliente.Sociedad;
        await Task.Delay(50);

        Assert.Contains(vm.ModelosDisponibles, m => m.Codigo == "200");
        Assert.DoesNotContain(vm.ModelosDisponibles, m => m.Codigo == "130");
    }

    [Fact]
    public async Task ViewModel_avisa_del_NIF_duplicado_y_no_guarda_si_se_cancela()
    {
        using var e = new EscenarioClientes();
        await e.CrearFisica("Ana", "12345678Z");
        var dialogos = new DialogosFalsos(false);
        var vm = new ClientesViewModel(e.Servicio, dialogos);
        await vm.AlEntrarAsync();

        await vm.NuevoCommand.ExecuteAsync(null);
        vm.Nombre = "Otra Ana";
        vm.Nif = "12345678Z";
        await vm.GuardarPanelCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogos.Veces);
        Assert.True(vm.PanelVisible);
        Assert.Single(await e.Servicio.ListarAsync());
    }

    [Fact]
    public async Task ViewModel_muestra_el_error_de_validacion()
    {
        using var e = new EscenarioClientes();
        var vm = new ClientesViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.NuevoCommand.ExecuteAsync(null);
        vm.Nombre = "Ana";
        vm.Nif = "12345678A";

        await vm.GuardarPanelCommand.ExecuteAsync(null);

        Assert.Equal("El NIF/CIF 12345678A no es válido.", vm.ErrorPanel);
    }
}
