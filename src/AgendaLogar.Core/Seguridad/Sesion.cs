using AgendaLogar.Core.Dominio;

namespace AgendaLogar.Core.Seguridad;

/// <summary>Datos del usuario que ha iniciado sesión (sin el hash de la contraseña).</summary>
public sealed record UsuarioSesion(int Id, string Nombre, string NombreUsuario, Rol Rol, bool DebeCambiarPassword)
{
    public bool EsAdmin => Rol == Rol.Admin;
}

/// <summary>Sesión actual de la aplicación. Hay una por instancia (aplicación de escritorio).</summary>
public interface ISesion
{
    UsuarioSesion? Usuario { get; }
    bool Iniciada { get; }
    event EventHandler? Cambiada;
    void Iniciar(UsuarioSesion usuario);
    void Cerrar();
}

public class Sesion : ISesion
{
    public UsuarioSesion? Usuario { get; private set; }
    public bool Iniciada => Usuario is not null;
    public event EventHandler? Cambiada;

    public void Iniciar(UsuarioSesion usuario)
    {
        Usuario = usuario;
        Cambiada?.Invoke(this, EventArgs.Empty);
    }

    public void Cerrar()
    {
        Usuario = null;
        Cambiada?.Invoke(this, EventArgs.Empty);
    }
}
