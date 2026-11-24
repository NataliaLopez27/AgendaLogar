using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Core.Datos;

/// <summary>
/// Comprueba la conexión con la base de datos y, si se pide, aplica las migraciones pendientes.
/// Reintenta varias veces porque Azure SQL serverless puede tardar en despertar (RNF-03c).
/// </summary>
public class InicializadorBaseDatos
{
    /// <summary>Usuario y contraseña de la primera cuenta, que hay que cambiar al entrar.</summary>
    public const string UsuarioAdminInicial = "admin";
    public const string PasswordAdminInicial = "admin";

    private readonly IDbContextFactory<AgendaLogarDbContext> _fabrica;
    private readonly IHashPassword _hash;

    public InicializadorBaseDatos(IDbContextFactory<AgendaLogarDbContext> fabrica, IHashPassword hash)
    {
        _fabrica = fabrica;
        _hash = hash;
    }

    public async Task InicializarAsync(bool aplicarMigraciones, int intentos = 5,
        TimeSpan? espera = null, CancellationToken ct = default)
    {
        var pausa = espera ?? TimeSpan.FromSeconds(5);

        for (var intento = 1; ; intento++)
        {
            try
            {
                await using var db = await _fabrica.CreateDbContextAsync(ct);
                if (aplicarMigraciones)
                    await db.Database.MigrateAsync(ct);
                else if (!await db.Database.CanConnectAsync(ct))
                    throw new InvalidOperationException("No se puede conectar con la base de datos.");
                await CrearAdministradorInicialAsync(db, ct);
                return;
            }
            catch (Exception) when (intento < intentos && !ct.IsCancellationRequested)
            {
                await Task.Delay(pausa, ct);
            }
        }
    }

    /// <summary>
    /// Si no hay ningún usuario, crea la cuenta "admin" / "admin" con rol ADMIN
    /// y obliga a cambiar la contraseña en el primer inicio de sesión.
    /// </summary>
    public async Task CrearAdministradorInicialAsync(AgendaLogarDbContext db, CancellationToken ct = default)
    {
        if (await db.Usuarios.AnyAsync(ct))
            return;

        db.Usuarios.Add(new Usuario
        {
            Nombre = "Administrador",
            NombreUsuario = UsuarioAdminInicial,
            HashPassword = _hash.Calcular(PasswordAdminInicial),
            Rol = Rol.Admin,
            Activo = true,
            DebeCambiarPassword = true
        });
        await db.SaveChangesAsync(ct);
    }
}
