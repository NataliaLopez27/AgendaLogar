using AgendaLogar.Core.Seguridad;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.Tests;

public class CU02_CambiarPasswordTests
{
    private static async Task<(ServicioAutenticacion servicio, Sesion sesion)> Conectar(BaseDatosPrueba bd, string usuario, string password)
    {
        var sesion = new Sesion();
        var servicio = new ServicioAutenticacion(bd, Hash.Rapido, sesion);
        await servicio.IniciarSesionAsync(usuario, password);
        return (servicio, sesion);
    }

    [Fact]
    public async Task Cambia_la_contrasena_y_solo_vale_la_nueva()
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuria", "antigua123");
        var (servicio, _) = await Conectar(bd, "nuria", "antigua123");

        await servicio.CambiarPasswordAsync("antigua123", "Nueva2026", "Nueva2026");

        servicio.CerrarSesion();
        await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.IniciarSesionAsync("nuria", "antigua123"));
        await servicio.IniciarSesionAsync("nuria", "Nueva2026");
    }

    [Theory]
    [InlineData("mala1234", "Nueva2026", "Nueva2026", "La contraseña actual no es correcta.")]
    [InlineData("antigua123", "Nueva2026", "Otra2026", "Las dos contraseñas nuevas no coinciden.")]
    [InlineData("antigua123", "antigua123", "antigua123", "La contraseña nueva tiene que ser distinta de la actual.")]
    [InlineData("antigua123", "corta1", "corta1", "La contraseña debe tener al menos 8 caracteres.")]
    [InlineData("antigua123", "sinnumeros", "sinnumeros", "La contraseña debe tener al menos una letra y un número.")]
    [InlineData("antigua123", "nuria2026", "nuria2026", "La contraseña no puede contener el nombre de usuario.")]
    public async Task Rechaza_cambios_no_validos(string actual, string nueva, string repetida, string mensaje)
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuria", "antigua123");
        var (servicio, _) = await Conectar(bd, "nuria", "antigua123");

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CambiarPasswordAsync(actual, nueva, repetida));

        Assert.Equal(mensaje, ex.Message);
    }

    [Fact]
    public async Task Sin_sesion_no_se_puede_cambiar()
    {
        using var bd = new BaseDatosPrueba();
        var servicio = new ServicioAutenticacion(bd, Hash.Rapido, new Sesion());

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => servicio.CambiarPasswordAsync("a", "Nueva2026", "Nueva2026"));
    }

    [Fact]
    public async Task Cambiarla_quita_la_obligacion_en_la_sesion_y_en_la_base_de_datos()
    {
        using var bd = new BaseDatosPrueba();
        var u = Datos.CrearUsuario(bd, "nuevo", "temporal1", debeCambiar: true);
        var (servicio, sesion) = await Conectar(bd, "nuevo", "temporal1");

        await servicio.CambiarPasswordAsync("temporal1", "Definitiva9", "Definitiva9");

        Assert.False(sesion.Usuario!.DebeCambiarPassword);
        using var db = bd.CreateDbContext();
        Assert.False(db.Usuarios.Single(x => x.Id == u.Id).DebeCambiarPassword);
    }

    [Fact]
    public async Task ViewModel_obligatorio_va_al_inicio_al_guardar()
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuevo", "temporal1", debeCambiar: true);
        var (servicio, sesion) = await Conectar(bd, "nuevo", "temporal1");
        var nav = new NavegacionFalsa();
        var vm = new CambiarPasswordViewModel(servicio, nav, sesion)
        {
            PasswordActual = "temporal1", PasswordNueva = "Definitiva9", PasswordRepetida = "Definitiva9"
        };

        Assert.True(vm.Obligatorio);
        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.Null(vm.Error);
        Assert.Equal(typeof(InicioViewModel), nav.Destino);
    }

    [Fact]
    public async Task ViewModel_voluntario_muestra_mensaje_y_vacia_los_campos()
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuria", "antigua123");
        var (servicio, sesion) = await Conectar(bd, "nuria", "antigua123");
        var nav = new NavegacionFalsa();
        var vm = new CambiarPasswordViewModel(servicio, nav, sesion)
        {
            PasswordActual = "antigua123", PasswordNueva = "Nueva2026", PasswordRepetida = "Nueva2026"
        };

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.False(vm.Obligatorio);
        Assert.Equal("Contraseña cambiada correctamente.", vm.Exito);
        Assert.Equal(string.Empty, vm.PasswordNueva);
        Assert.Null(nav.Destino);
    }

    [Fact]
    public async Task ViewModel_muestra_el_error()
    {
        using var bd = new BaseDatosPrueba();
        Datos.CrearUsuario(bd, "nuria", "antigua123");
        var (servicio, sesion) = await Conectar(bd, "nuria", "antigua123");
        var vm = new CambiarPasswordViewModel(servicio, new NavegacionFalsa(), sesion)
        {
            PasswordActual = "antigua123", PasswordNueva = "Nueva2026", PasswordRepetida = "Distinta1"
        };

        await vm.GuardarCommand.ExecuteAsync(null);

        Assert.Equal("Las dos contraseñas nuevas no coinciden.", vm.Error);
    }
}
