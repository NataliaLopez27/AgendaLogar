using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Core.Servicios;

/// <summary>Fila del listado de empleados.</summary>
public sealed record UsuarioResumen(int Id, string Nombre, string NombreUsuario, Rol Rol, bool Activo, bool DebeCambiarPassword)
{
    public string RolTexto => Rol == Rol.Admin ? "Administrador" : "Empleado";
    public string EstadoTexto => Activo ? "Activo" : "Desactivado";
}

/// <summary>Datos que se rellenan al crear o editar un empleado.</summary>
public sealed record DatosUsuario(string Nombre, string NombreUsuario, Rol Rol);

/// <summary>CU-10 Gestionar empleados (RF-30, RF-31). Todas las operaciones son solo para el administrador.</summary>
public class ServicioUsuarios
{
    private readonly IDbContextFactory<AgendaLogarDbContext> _fabrica;
    private readonly IHashPassword _hash;
    private readonly ControlAcceso _acceso;

    public ServicioUsuarios(IDbContextFactory<AgendaLogarDbContext> fabrica, IHashPassword hash, ControlAcceso acceso)
    {
        _fabrica = fabrica;
        _hash = hash;
        _acceso = acceso;
    }

    public async Task<IReadOnlyList<UsuarioResumen>> ListarAsync(bool incluirDesactivados, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        return await db.Usuarios.AsNoTracking()
            .Where(u => incluirDesactivados || u.Activo)
            .OrderByDescending(u => u.Activo).ThenBy(u => u.Nombre)
            .Select(u => new UsuarioResumen(u.Id, u.Nombre, u.NombreUsuario, u.Rol, u.Activo, u.DebeCambiarPassword))
            .ToListAsync(ct);
    }

    /// <summary>Crea un usuario. Tendrá que cambiar la contraseña inicial al entrar.</summary>
    public async Task<UsuarioResumen> CrearAsync(DatosUsuario datos, string passwordInicial, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var (nombre, nombreUsuario) = Validar(datos);
        if (ReglasPassword.Validar(passwordInicial, nombreUsuario) is string motivo)
            throw new ReglaNegocioException(motivo);
        await ComprobarNombreUsuarioLibreAsync(db, nombreUsuario, idActual: null, ct);

        var usuario = new Usuario
        {
            Nombre = nombre,
            NombreUsuario = nombreUsuario,
            Rol = datos.Rol,
            Activo = true,
            HashPassword = _hash.Calcular(passwordInicial),
            DebeCambiarPassword = true
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);
        return Resumen(usuario);
    }

    public async Task<UsuarioResumen> EditarAsync(int id, DatosUsuario datos, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var admin = await _acceso.RequerirAdminAsync(db, ct);

        var usuario = await BuscarAsync(db, id, ct);
        var (nombre, nombreUsuario) = Validar(datos);
        await ComprobarNombreUsuarioLibreAsync(db, nombreUsuario, id, ct);

        if (usuario.Rol == Rol.Admin && datos.Rol != Rol.Admin)
        {
            if (usuario.Id == admin.Id)
                throw new ReglaNegocioException("No puedes quitarte a ti mismo el rol de administrador.");
            await ComprobarQueQuedaOtroAdminAsync(db, usuario.Id, ct);
        }

        usuario.Nombre = nombre;
        usuario.NombreUsuario = nombreUsuario;
        usuario.Rol = datos.Rol;
        await db.SaveChangesAsync(ct);
        return Resumen(usuario);
    }

    /// <summary>
    /// Desactiva un usuario (no se borra, para conservar el histórico).
    /// No se permite si tiene asignaciones en un periodo abierto (regla de CU-10).
    /// </summary>
    public async Task DesactivarAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var admin = await _acceso.RequerirAdminAsync(db, ct);

        var usuario = await BuscarAsync(db, id, ct);
        if (!usuario.Activo) return;
        if (usuario.Id == admin.Id)
            throw new ReglaNegocioException("No puedes desactivar tu propia cuenta.");
        if (usuario.Rol == Rol.Admin)
            await ComprobarQueQuedaOtroAdminAsync(db, usuario.Id, ct);

        var pendientes = await db.Asignaciones
            .CountAsync(a => a.UsuarioId == id && a.Periodo.Estado == EstadoPeriodo.Abierto, ct);
        if (pendientes > 0)
            throw new ReglaNegocioException(
                $"{usuario.Nombre} tiene {pendientes} cliente(s) asignado(s) en periodos abiertos. Reasígnalos antes de desactivarlo.");

        usuario.Activo = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task ReactivarAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var usuario = await BuscarAsync(db, id, ct);
        usuario.Activo = true;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>RF-31: pone una contraseña temporal; el usuario tendrá que cambiarla al entrar.</summary>
    public async Task RestablecerPasswordAsync(int id, string passwordTemporal, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var usuario = await BuscarAsync(db, id, ct);
        if (ReglasPassword.Validar(passwordTemporal, usuario.NombreUsuario) is string motivo)
            throw new ReglaNegocioException(motivo);

        usuario.HashPassword = _hash.Calcular(passwordTemporal);
        usuario.DebeCambiarPassword = true;
        await db.SaveChangesAsync(ct);
    }

    private static (string nombre, string nombreUsuario) Validar(DatosUsuario datos)
    {
        var nombre = datos.Nombre?.Trim() ?? string.Empty;
        var nombreUsuario = datos.NombreUsuario?.Trim() ?? string.Empty;

        if (nombre.Length == 0)
            throw new ReglaNegocioException("Indica el nombre del empleado.");
        if (nombre.Length > 100)
            throw new ReglaNegocioException("El nombre no puede tener más de 100 caracteres.");
        if (nombreUsuario.Length < 3 || nombreUsuario.Length > 50)
            throw new ReglaNegocioException("El usuario debe tener entre 3 y 50 caracteres.");
        if (!nombreUsuario.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-'))
            throw new ReglaNegocioException("El usuario solo puede tener letras, números, puntos, guiones y guiones bajos.");
        if (!Enum.IsDefined(datos.Rol))
            throw new ReglaNegocioException("El rol no es válido.");

        return (nombre, nombreUsuario);
    }

    private static async Task ComprobarNombreUsuarioLibreAsync(AgendaLogarDbContext db, string nombreUsuario, int? idActual, CancellationToken ct)
    {
        var minusculas = nombreUsuario.ToLower();
        var ocupado = await db.Usuarios.AnyAsync(u => u.NombreUsuario.ToLower() == minusculas && u.Id != idActual, ct);
        if (ocupado)
            throw new ReglaNegocioException($"Ya existe un usuario \"{nombreUsuario}\".");
    }

    private static async Task ComprobarQueQuedaOtroAdminAsync(AgendaLogarDbContext db, int idQueDejaDeSerAdmin, CancellationToken ct)
    {
        var otros = await db.Usuarios.AnyAsync(u => u.Id != idQueDejaDeSerAdmin && u.Activo && u.Rol == Rol.Admin, ct);
        if (!otros)
            throw new ReglaNegocioException("Tiene que quedar al menos un administrador activo.");
    }

    private static async Task<Usuario> BuscarAsync(AgendaLogarDbContext db, int id, CancellationToken ct) =>
        await db.Usuarios.SingleOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new ReglaNegocioException("El usuario no existe.");

    private static UsuarioResumen Resumen(Usuario u) =>
        new(u.Id, u.Nombre, u.NombreUsuario, u.Rol, u.Activo, u.DebeCambiarPassword);
}
