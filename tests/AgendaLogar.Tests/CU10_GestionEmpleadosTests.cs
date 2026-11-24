using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.Navegacion;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.Tests;

public class CU10_GestionEmpleadosTests
{
    private sealed class Escenario : IDisposable
    {
        public BaseDatosPrueba Bd { get; } = new();
        public Usuario Admin { get; }
        public ServicioUsuarios Servicio { get; }

        public Escenario()
        {
            Admin = Datos.CrearUsuario(Bd, "jefe", "secreta123", Rol.Admin, nombre: "Jefe");
            Servicio = new ServicioUsuarios(Bd, Hash.Rapido, new ControlAcceso(Datos.SesionDe(Admin)));
        }

        public ServicioAutenticacion Autenticacion() => new(Bd, Hash.Rapido, new Sesion());
        public void Dispose() => Bd.Dispose();
    }

    [Fact]
    public async Task El_admin_crea_un_empleado_que_debe_cambiar_la_contrasena_al_entrar()
    {
        using var e = new Escenario();

        var nuevo = await e.Servicio.CrearAsync(new DatosUsuario(" Marisa ", "marisa", Rol.Empleado), "Temporal1");

        Assert.Equal("Marisa", nuevo.Nombre);
        Assert.True(nuevo.Activo);
        var sesion = await e.Autenticacion().IniciarSesionAsync("marisa", "Temporal1");
        Assert.True(sesion.DebeCambiarPassword);
    }

    [Theory]
    [InlineData("", "marisa", "Temporal1", "Indica el nombre del empleado.")]
    [InlineData("Marisa", "ma", "Temporal1", "El usuario debe tener entre 3 y 50 caracteres.")]
    [InlineData("Marisa", "mari sa", "Temporal1", "El usuario solo puede tener letras, números, puntos, guiones y guiones bajos.")]
    [InlineData("Marisa", "marisa", "corta", "La contraseña debe tener al menos 8 caracteres.")]
    [InlineData("Otro", "JEFE", "Temporal1", "Ya existe un usuario \"JEFE\".")]
    public async Task Valida_los_datos_al_crear(string nombre, string usuario, string password, string mensaje)
    {
        using var e = new Escenario();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => e.Servicio.CrearAsync(new DatosUsuario(nombre, usuario, Rol.Empleado), password));

        Assert.Equal(mensaje, ex.Message);
    }

    [Fact]
    public async Task Edita_nombre_usuario_y_rol()
    {
        using var e = new Escenario();
        var u = await e.Servicio.CrearAsync(new DatosUsuario("Javi", "javi", Rol.Empleado), "Temporal1");

        var editado = await e.Servicio.EditarAsync(u.Id, new DatosUsuario("Javier", "javier", Rol.Admin));

        Assert.Equal("Javier", editado.Nombre);
        Assert.Equal("javier", editado.NombreUsuario);
        Assert.Equal(Rol.Admin, editado.Rol);
    }

    [Fact]
    public async Task El_admin_no_puede_quitarse_su_propio_rol()
    {
        using var e = new Escenario();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => e.Servicio.EditarAsync(e.Admin.Id, new DatosUsuario("Jefe", "jefe", Rol.Empleado)));

        Assert.Equal("No puedes quitarte a ti mismo el rol de administrador.", ex.Message);
    }

    [Fact]
    public async Task Desactivar_impide_iniciar_sesion_y_reactivar_lo_permite_otra_vez()
    {
        using var e = new Escenario();
        var u = await e.Servicio.CrearAsync(new DatosUsuario("Javi", "javi", Rol.Empleado), "Temporal1");

        await e.Servicio.DesactivarAsync(u.Id);
        await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Autenticacion().IniciarSesionAsync("javi", "Temporal1"));
        Assert.DoesNotContain(await e.Servicio.ListarAsync(incluirDesactivados: false), x => x.Id == u.Id);
        Assert.Contains(await e.Servicio.ListarAsync(incluirDesactivados: true), x => x.Id == u.Id && !x.Activo);

        await e.Servicio.ReactivarAsync(u.Id);
        await e.Autenticacion().IniciarSesionAsync("javi", "Temporal1");
    }

    [Fact]
    public async Task No_se_puede_desactivar_a_uno_mismo()
    {
        using var e = new Escenario();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.DesactivarAsync(e.Admin.Id));

        Assert.Equal("No puedes desactivar tu propia cuenta.", ex.Message);
    }

    [Fact]
    public async Task No_se_puede_desactivar_con_asignaciones_en_un_periodo_abierto()
    {
        using var e = new Escenario();
        var u = await e.Servicio.CrearAsync(new DatosUsuario("Nuria", "nuria", Rol.Empleado), "Temporal1");
        using (var db = e.Bd.CreateDbContext())
        {
            var cliente = new Cliente { Nombre = "Cliente", Nif = "1", Tipo = TipoCliente.Fisica };
            var periodo = new Periodo { Anio = 2026, Trimestre = 4, Estado = EstadoPeriodo.Abierto };
            db.AddRange(cliente, periodo);
            db.SaveChanges();
            db.Asignaciones.Add(new Asignacion { ClienteId = cliente.Id, PeriodoId = periodo.Id, UsuarioId = u.Id });
            db.SaveChanges();
        }

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => e.Servicio.DesactivarAsync(u.Id));
        Assert.Contains("Reasígnalos antes de desactivarlo", ex.Message);

        // Con el periodo cerrado ya se puede.
        using (var db = e.Bd.CreateDbContext())
        {
            db.Periodos.Single().Estado = EstadoPeriodo.Cerrado;
            db.SaveChanges();
        }
        await e.Servicio.DesactivarAsync(u.Id);
    }

    [Fact]
    public async Task Restablecer_la_contrasena_obliga_a_cambiarla()
    {
        using var e = new Escenario();
        var u = Datos.CrearUsuario(e.Bd, "nuria", "antigua123");

        await e.Servicio.RestablecerPasswordAsync(u.Id, "Temporal9");

        var sesion = await e.Autenticacion().IniciarSesionAsync("nuria", "Temporal9");
        Assert.True(sesion.DebeCambiarPassword);
    }

    [Fact]
    public async Task Un_empleado_no_puede_usar_la_gestion_de_empleados()
    {
        using var bd = new BaseDatosPrueba();
        var empleado = Datos.CrearUsuario(bd, "nuria", "secreta123");
        var servicio = new ServicioUsuarios(bd, Hash.Rapido, new ControlAcceso(Datos.SesionDe(empleado)));

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => servicio.ListarAsync(true));
        await Assert.ThrowsAsync<AccesoDenegadoException>(
            () => servicio.CrearAsync(new DatosUsuario("X", "xxx", Rol.Admin), "Temporal1"));
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => servicio.RestablecerPasswordAsync(empleado.Id, "Temporal1"));
    }

    [Fact]
    public void La_contrasena_generada_cumple_las_reglas()
    {
        for (var i = 0; i < 200; i++)
            Assert.Null(ReglasPassword.Validar(GeneradorPassword.Generar()));
    }

    private sealed class DialogosFalsos(bool respuesta) : IDialogos
    {
        public bool Confirmar(string titulo, string mensaje) => respuesta;
    }

    [Fact]
    public async Task ViewModel_alta_completa_desde_el_formulario()
    {
        using var e = new Escenario();
        var vm = new EmpleadosViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();

        vm.NuevoCommand.Execute(null);
        Assert.True(vm.EditorVisible);
        Assert.Null(ReglasPassword.Validar(vm.PasswordInicial));
        vm.Nombre = "Guillermo";
        vm.NombreUsuario = "guillermo";
        var password = vm.PasswordInicial;
        await vm.GuardarEditorCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorEditor);
        Assert.False(vm.EditorVisible);
        Assert.Contains(vm.Usuarios, u => u.NombreUsuario == "guillermo");
        Assert.Contains(password, vm.Aviso);
        Assert.Equal("guillermo", vm.Seleccionado?.NombreUsuario);
    }

    [Fact]
    public async Task ViewModel_no_desactiva_si_se_cancela_la_confirmacion()
    {
        using var e = new Escenario();
        var u = await e.Servicio.CrearAsync(new DatosUsuario("Javi", "javi", Rol.Empleado), "Temporal1");
        var vm = new EmpleadosViewModel(e.Servicio, new DialogosFalsos(false));
        await vm.AlEntrarAsync();
        vm.Seleccionado = vm.Usuarios.Single(x => x.Id == u.Id);

        await vm.DesactivarCommand.ExecuteAsync(null);

        Assert.True(vm.Usuarios.Single(x => x.Id == u.Id).Activo);
    }

    [Fact]
    public async Task ViewModel_restablecer_muestra_la_contrasena_temporal()
    {
        using var e = new Escenario();
        var u = await e.Servicio.CrearAsync(new DatosUsuario("Javi", "javi", Rol.Empleado), "Temporal1");
        var vm = new EmpleadosViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();
        vm.Seleccionado = vm.Usuarios.Single(x => x.Id == u.Id);

        await vm.RestablecerPasswordCommand.ExecuteAsync(null);

        Assert.StartsWith("Nueva contraseña temporal de Javi: ", vm.Aviso);
        var temporal = vm.Aviso!.Split(": ")[1];
        await e.Autenticacion().IniciarSesionAsync("javi", temporal);
    }

    [Fact]
    public async Task ViewModel_muestra_el_error_de_la_regla_de_negocio()
    {
        using var e = new Escenario();
        var vm = new EmpleadosViewModel(e.Servicio, new DialogosFalsos(true));
        await vm.AlEntrarAsync();
        vm.Seleccionado = vm.Usuarios.Single(x => x.Id == e.Admin.Id);

        await vm.DesactivarCommand.ExecuteAsync(null);

        Assert.Equal("No puedes desactivar tu propia cuenta.", vm.Error);
    }
}
