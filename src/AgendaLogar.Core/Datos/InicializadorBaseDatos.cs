using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Core.Datos;

/// <summary>
/// Comprueba la conexión con la base de datos y, si se pide, aplica las migraciones pendientes.
/// Reintenta varias veces porque Azure SQL serverless puede tardar en despertar (RNF-03c).
/// </summary>
public class InicializadorBaseDatos
{
    private readonly IDbContextFactory<AgendaLogarDbContext> _fabrica;

    public InicializadorBaseDatos(IDbContextFactory<AgendaLogarDbContext> fabrica)
    {
        _fabrica = fabrica;
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
                return;
            }
            catch (Exception) when (intento < intentos && !ct.IsCancellationRequested)
            {
                await Task.Delay(pausa, ct);
            }
        }
    }
}
