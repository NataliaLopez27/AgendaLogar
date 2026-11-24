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
}
