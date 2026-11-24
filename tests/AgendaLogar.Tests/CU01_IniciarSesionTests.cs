using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.Tests;

public class CU01_IniciarSesionTests
{
    private static (ServicioAutenticacion servicio, Sesion sesion) Crear(BaseDatosPrueba bd)
    {
        var sesion = new Sesion();
        return (new ServicioAutenticacion(bd, Hash.Rapido, sesion), sesion);
    }

    [Fact]
    public async Task Con_credenciales_correctas_inicia_la_sesion()
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuria", "secreta123", nombre: "Nuria");
        var (servicio, sesion) = Crear(bd);

        var usuario = await servicio.IniciarSesionAsync("nuria", "secreta123");

        Assert.True(sesion.Iniciada);
        Assert.Equal("Nuria", usuario.Nombre);
        Assert.Equal(Rol.Empleado, usuario.Rol);
    }

    [Theory]
    [InlineData("nuria", "mala")]        // contraseña incorrecta
    [InlineData("noexiste", "secreta123")] // usuario inexistente
    public async Task Con_credenciales_incorrectas_da_un_mensaje_generico(string usuario, string password)
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuria", "secreta123");
        var (servicio, sesion) = Crear(bd);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.IniciarSesionAsync(usuario, password));

        Assert.Equal(ServicioAutenticacion.MensajeCredencialesIncorrectas, ex.Message);
        Assert.False(sesion.Iniciada);
    }

    [Fact]
    public async Task Un_usuario_desactivado_no_puede_entrar_RF04()
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "javi", "secreta123", activo: false);
        var (servicio, sesion) = Crear(bd);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.IniciarSesionAsync("javi", "secreta123"));

        Assert.Equal(ServicioAutenticacion.MensajeCredencialesIncorrectas, ex.Message);
        Assert.False(sesion.Iniciada);
    }

    [Fact]
    public async Task Cerrar_sesion_borra_el_usuario_actual()
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuria", "secreta123");
        var (servicio, sesion) = Crear(bd);
        await servicio.IniciarSesionAsync("nuria", "secreta123");

        servicio.CerrarSesion();

        Assert.False(sesion.Iniciada);
    }

    [Fact]
    public async Task La_primera_vez_se_crea_el_admin_inicial_que_debe_cambiar_la_contrasena()
    {
        using var bd = new BaseDatosPrueba();
        var inicializador = new InicializadorBaseDatos(bd, Hash.Rapido);
        await using (var db = bd.CreateDbContext())
            await inicializador.CrearAdministradorInicialAsync(db);
        var (servicio, _) = Crear(bd);

        var admin = await servicio.IniciarSesionAsync(InicializadorBaseDatos.UsuarioAdminInicial, InicializadorBaseDatos.PasswordAdminInicial);

        Assert.True(admin.EsAdmin);
        Assert.True(admin.DebeCambiarPassword);
    }

    [Fact]
    public async Task El_admin_inicial_no_se_crea_si_ya_hay_usuarios()
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuria", "secreta123");
        var inicializador = new InicializadorBaseDatos(bd, Hash.Rapido);
        await using var db = bd.CreateDbContext();

        await inicializador.CrearAdministradorInicialAsync(db);

        Assert.Equal(1, db.Usuarios.Count());
    }

    [Fact]
    public async Task LoginViewModel_va_al_inicio_si_todo_va_bien()
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuria", "secreta123");
        var (servicio, _) = Crear(bd);
        var nav = new NavegacionFalsa();
        var vm = new LoginViewModel(servicio, nav) { NombreUsuario = "nuria", Password = "secreta123" };

        await vm.IniciarSesionCommand.ExecuteAsync(null);

        Assert.Null(vm.Error);
        Assert.Equal(typeof(InicioViewModel), nav.Destino);
        Assert.Equal(string.Empty, vm.Password);
    }

    [Fact]
    public async Task LoginViewModel_obliga_a_cambiar_la_contrasena_si_hace_falta()
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuevo", "temporal1", debeCambiar: true);
        var (servicio, _) = Crear(bd);
        var nav = new NavegacionFalsa();
        var vm = new LoginViewModel(servicio, nav) { NombreUsuario = "nuevo", Password = "temporal1" };

        await vm.IniciarSesionCommand.ExecuteAsync(null);

        Assert.Equal(typeof(CambiarPasswordViewModel), nav.Destino);
    }

    [Fact]
    public async Task LoginViewModel_muestra_el_error_y_no_navega()
    {
        using var bd = new BaseDatosPrueba();
        var (servicio, _) = Crear(bd);
        var nav = new NavegacionFalsa();
        var vm = new LoginViewModel(servicio, nav) { NombreUsuario = "x", Password = "y" };

        await vm.IniciarSesionCommand.ExecuteAsync(null);

        Assert.Equal(ServicioAutenticacion.MensajeCredencialesIncorrectas, vm.Error);
        Assert.Null(nav.Destino);
    }

    [Fact]
    public void No_se_puede_pulsar_entrar_sin_usuario_y_contrasena()
    {
        using var bd = new BaseDatosPrueba();
        var (servicio, _) = Crear(bd);
        var vm = new LoginViewModel(servicio, new NavegacionFalsa());

        Assert.False(vm.IniciarSesionCommand.CanExecute(null));
        vm.NombreUsuario = "nuria";
        vm.Password = "x";
        Assert.True(vm.IniciarSesionCommand.CanExecute(null));
    }
}
