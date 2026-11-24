using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.Navegacion;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.Tests;

/// <summary>Issue 6: roles y control de acceso en la capa de servicios (RF-02, RNF-04b).</summary>
public class Hito1_ControlAccesoTests
{
    [Fact]
    public void Sin_sesion_no_se_puede_hacer_nada()
    {
        var acceso = new ControlAcceso(new Sesion());
        Assert.Throws<AccesoDenegadoException>(() => acceso.RequerirSesion());
    }

    [Fact]
    public async Task Un_empleado_no_pasa_la_comprobacion_de_administrador()
    {
        using var bd = new BaseDatosPrueba();
        var empleado = Datos.CrearUsuario(bd, "nuria", "secreta123");
        var acceso = new ControlAcceso(Datos.SesionDe(empleado));
        await using var db = bd.CreateDbContext();

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => acceso.RequerirAdminAsync(db));
    }

    [Fact]
    public async Task El_administrador_pasa_la_comprobacion()
    {
        using var bd = new BaseDatosPrueba();
        var admin = Datos.CrearUsuario(bd, "jefe", "secreta123", Rol.Admin);
        var acceso = new ControlAcceso(Datos.SesionDe(admin));
        await using var db = bd.CreateDbContext();

        var usuario = await acceso.RequerirAdminAsync(db);

        Assert.Equal(admin.Id, usuario.Id);
    }

    [Fact]
    public async Task Un_admin_desactivado_desde_otro_puesto_pierde_el_permiso_al_momento()
    {
        using var bd = new BaseDatosPrueba();
        var admin = Datos.CrearUsuario(bd, "jefe", "secreta123", Rol.Admin);
        var acceso = new ControlAcceso(Datos.SesionDe(admin));
        await using var db = bd.CreateDbContext();
        db.Usuarios.Single(u => u.Id == admin.Id).Activo = false;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => acceso.RequerirAdminAsync(db));
    }

    [Fact]
    public void Un_empleado_no_puede_tocar_lo_de_otro_pero_el_admin_si()
    {
        var empleado = new Sesion();
        empleado.Iniciar(new UsuarioSesion(1, "Nuria", "nuria", Rol.Empleado, false));
        var admin = new Sesion();
        admin.Iniciar(new UsuarioSesion(9, "Jefe", "jefe", Rol.Admin, false));

        new ControlAcceso(empleado).RequerirPropietarioOAdmin(1);
        Assert.Throws<AccesoDenegadoException>(() => new ControlAcceso(empleado).RequerirPropietarioOAdmin(2));
        new ControlAcceso(admin).RequerirPropietarioOAdmin(2);
    }

    [Theory]
    [InlineData(null, typeof(InicioViewModel), typeof(LoginViewModel))]
    [InlineData(null, typeof(LoginViewModel), typeof(LoginViewModel))]
    [InlineData(Rol.Empleado, typeof(CatalogoModelosViewModel), typeof(InicioViewModel))]
    [InlineData(Rol.Empleado, typeof(InicioViewModel), typeof(InicioViewModel))]
    [InlineData(Rol.Admin, typeof(CatalogoModelosViewModel), typeof(CatalogoModelosViewModel))]
    public void La_navegacion_redirige_si_no_hay_permiso(Rol? rol, Type pedido, Type esperado)
    {
        var sesion = new Sesion();
        if (rol is Rol r) sesion.Iniciar(new UsuarioSesion(1, "X", "x", r, false));
        var nav = new NavegacionService(_ => throw new NotImplementedException(), sesion);

        Assert.Equal(esperado, nav.DestinoPermitido(pedido));
    }
}
