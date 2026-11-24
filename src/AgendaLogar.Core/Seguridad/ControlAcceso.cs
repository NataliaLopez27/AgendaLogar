using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Servicios;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Core.Seguridad;

/// <summary>
/// Comprobaciones de permisos que hacen los servicios antes de cada operación (RF-02, RNF-04b).
/// No basta con ocultar botones: aunque alguien llegue a llamar al servicio, se rechaza.
/// </summary>
public class ControlAcceso
{
    private readonly ISesion _sesion;

    public ControlAcceso(ISesion sesion)
    {
        _sesion = sesion;
    }

    /// <summary>Devuelve el usuario de la sesión o lanza <see cref="AccesoDenegadoException"/> si no hay sesión.</summary>
    public UsuarioSesion RequerirSesion() =>
        _sesion.Usuario ?? throw new AccesoDenegadoException("Tienes que iniciar sesión.");

    /// <summary>
    /// Exige que el usuario actual sea administrador. Además de mirar la sesión,
    /// comprueba en la base de datos que sigue activo y con rol ADMIN (por si se cambió desde otro puesto).
    /// </summary>
    public async Task<UsuarioSesion> RequerirAdminAsync(AgendaLogarDbContext db, CancellationToken ct = default)
    {
        var usuario = RequerirSesion();
        if (!usuario.EsAdmin)
            throw new AccesoDenegadoException("Solo el administrador puede realizar esta operación.");

        var sigueSiendoAdmin = await db.Usuarios.AnyAsync(u => u.Id == usuario.Id && u.Activo && u.Rol == Rol.Admin, ct);
        if (!sigueSiendoAdmin)
            throw new AccesoDenegadoException("Solo el administrador puede realizar esta operación.");

        return usuario;
    }

    /// <summary>
    /// Un empleado solo puede trabajar con lo suyo; el administrador, con lo de cualquiera.
    /// Se usará en los servicios de asignaciones (hito 4).
    /// </summary>
    public void RequerirPropietarioOAdmin(int? usuarioIdPropietario)
    {
        var usuario = RequerirSesion();
        if (!usuario.EsAdmin && usuario.Id != usuarioIdPropietario)
            throw new AccesoDenegadoException("No puedes ver ni modificar las asignaciones de otro empleado.");
    }
}
