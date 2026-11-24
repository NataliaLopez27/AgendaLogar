using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using AgendaLogar.Presentacion.Navegacion;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.Tests;

/// <summary>BCrypt con coste mínimo para que los tests vayan rápido.</summary>
public static class Hash
{
    public static readonly IHashPassword Rapido = new HashPasswordBCrypt(coste: 4);
}

public static class Datos
{
    public static Usuario CrearUsuario(BaseDatosPrueba bd, string usuario, string password,
        Rol rol = Rol.Empleado, bool activo = true, bool debeCambiar = false, string? nombre = null)
    {
        using var db = bd.CreateDbContext();
        var u = new Usuario
        {
            Nombre = nombre ?? usuario,
            NombreUsuario = usuario,
            HashPassword = Hash.Rapido.Calcular(password),
            Rol = rol,
            Activo = activo,
            DebeCambiarPassword = debeCambiar
        };
        db.Usuarios.Add(u);
        db.SaveChanges();
        return u;
    }

    public static ISesion SesionDe(Usuario u)
    {
        var s = new Sesion();
        s.Iniciar(new UsuarioSesion(u.Id, u.Nombre, u.NombreUsuario, u.Rol, u.DebeCambiarPassword));
        return s;
    }
}

/// <summary>Navegación falsa que solo recuerda a qué ViewModel se ha ido.</summary>
public class NavegacionFalsa : INavegacionService
{
    public Type? Destino { get; private set; }
    public ViewModelBase? VistaActual => null;
    public event EventHandler? VistaCambiada { add { } remove { } }
    public void NavegarA<TViewModel>() where TViewModel : ViewModelBase => Destino = typeof(TViewModel);
}
