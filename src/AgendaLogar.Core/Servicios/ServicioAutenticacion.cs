using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Core.Servicios;

/// <summary>Inicio y cierre de sesión (CU-01).</summary>
public class ServicioAutenticacion
{
    /// <summary>Mensaje genérico: no se dice si falla el usuario, la contraseña o si está desactivado.</summary>
    public const string MensajeCredencialesIncorrectas = "Usuario o contraseña incorrectos.";

    private readonly IDbContextFactory<AgendaLogarDbContext> _fabrica;
    private readonly IHashPassword _hash;
    private readonly ISesion _sesion;

    public ServicioAutenticacion(IDbContextFactory<AgendaLogarDbContext> fabrica, IHashPassword hash, ISesion sesion)
    {
        _fabrica = fabrica;
        _hash = hash;
        _sesion = sesion;
    }

    /// <summary>
    /// Valida las credenciales y que el usuario esté activo (RF-01, RF-04).
    /// Si son correctas inicia la sesión; si no, lanza <see cref="ReglaNegocioException"/> con un mensaje genérico.
    /// </summary>
    public async Task<UsuarioSesion> IniciarSesionAsync(string nombreUsuario, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrEmpty(password))
            throw new ReglaNegocioException("Introduce el usuario y la contraseña.");

        var nombre = nombreUsuario.Trim();
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var usuario = await db.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.NombreUsuario == nombre, ct);

        if (usuario is null || !usuario.Activo || !_hash.Verificar(password, usuario.HashPassword))
            throw new ReglaNegocioException(MensajeCredencialesIncorrectas);

        var sesion = new UsuarioSesion(usuario.Id, usuario.Nombre, usuario.NombreUsuario, usuario.Rol, usuario.DebeCambiarPassword);
        _sesion.Iniciar(sesion);
        return sesion;
    }

    public void CerrarSesion() => _sesion.Cerrar();

    /// <summary>
    /// CU-02: el usuario conectado cambia su contraseña (RF-03).
    /// Pide la actual y la nueva dos veces. Quita la obligación de cambiarla.
    /// </summary>
    public async Task CambiarPasswordAsync(string actual, string nueva, string repetida, CancellationToken ct = default)
    {
        var usuarioSesion = _sesion.Usuario ?? throw new AccesoDenegadoException("Tienes que iniciar sesión.");

        if (string.IsNullOrEmpty(actual))
            throw new ReglaNegocioException("Introduce tu contraseña actual.");
        if (nueva != repetida)
            throw new ReglaNegocioException("Las dos contraseñas nuevas no coinciden.");
        if (nueva == actual)
            throw new ReglaNegocioException("La contraseña nueva tiene que ser distinta de la actual.");
        if (ReglasPassword.Validar(nueva, usuarioSesion.NombreUsuario) is string motivo)
            throw new ReglaNegocioException(motivo);

        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Id == usuarioSesion.Id && u.Activo, ct)
            ?? throw new AccesoDenegadoException("Tu usuario ya no está activo.");

        if (!_hash.Verificar(actual, usuario.HashPassword))
            throw new ReglaNegocioException("La contraseña actual no es correcta.");

        usuario.HashPassword = _hash.Calcular(nueva);
        usuario.DebeCambiarPassword = false;
        await db.SaveChangesAsync(ct);

        _sesion.Iniciar(usuarioSesion with { DebeCambiarPassword = false });
    }
}
