using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Dominio;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Core.Importacion;

/// <summary>Resumen de una importación.</summary>
public sealed class ResultadoImportacion
{
    public List<ClienteImportado> Creados { get; } = new();
    public List<(ClienteImportado Cliente, string Motivo)> Omitidos { get; } = new();
    public bool Aplicado { get; init; }
}

/// <summary>
/// Guarda en la base de datos los clientes leídos de los cuadrantes (RF-60).
/// Es un proceso de un solo uso que ejecuta quien administra la base de datos, por eso no pasa por la sesión de la aplicación.
/// Se puede repetir: los clientes que ya existen no se duplican.
/// </summary>
public class ImportadorClientes
{
    private readonly IDbContextFactory<AgendaLogarDbContext> _fabrica;

    public ImportadorClientes(IDbContextFactory<AgendaLogarDbContext> fabrica)
    {
        _fabrica = fabrica;
    }

    /// <param name="aplicar">false = simulación: calcula el resultado sin guardar nada.</param>
    public async Task<ResultadoImportacion> ImportarAsync(IEnumerable<ClienteImportado> clientes, bool aplicar, CancellationToken ct = default)
    {
        var resultado = new ResultadoImportacion { Aplicado = aplicar };
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var existentes = await db.Clientes.AsNoTracking().Select(c => new { c.Nif, c.Nombre, c.Tipo }).ToListAsync(ct);
        var nifs = existentes.Where(c => c.Nif.Length > 0).Select(c => c.Nif).ToHashSet();
        var nombres = existentes.Select(c => Clave(c.Nombre, c.Tipo)).ToHashSet();

        // El mismo cliente puede venir en varios cuadrantes (de distintos trimestres o empleados).
        var nuevos = new Dictionary<string, (ClienteImportado Importado, Cliente Entidad)>();

        foreach (var c in clientes)
        {
            var clave = c.Nif.Length > 0 ? $"NIF:{c.Nif}" : Clave(c.Nombre, c.Tipo);

            if ((c.Nif.Length > 0 && nifs.Contains(c.Nif)) || (c.Nif.Length == 0 && nombres.Contains(Clave(c.Nombre, c.Tipo))))
            {
                resultado.Omitidos.Add((c, "ya existía en la base de datos"));
                continue;
            }

            if (nuevos.TryGetValue(clave, out var previo))
            {
                // Se añaden los modelos que no estuvieran ya.
                var añadidos = 0;
                foreach (var m in c.Modelos.Where(m => previo.Entidad.Modelos.All(x => x.ModeloCodigo != m.Codigo)))
                {
                    previo.Entidad.Modelos.Add(NuevoModelo(m));
                    añadidos++;
                }
                resultado.Omitidos.Add((c, $"repetido en {previo.Importado.Ubicacion}" + (añadidos > 0 ? $" (se añaden {añadidos} modelo(s))" : "")));
                continue;
            }

            var entidad = new Cliente
            {
                Nombre = c.Nombre.Length > 200 ? c.Nombre[..200] : c.Nombre,
                Nif = c.Nif,
                Tipo = c.Tipo,
                Notas = c.Avisos.Count > 0 ? "Importado del cuadrante. Revisar: " + string.Join(" ", c.Avisos) : null,
                Altas = { new AltaCliente { FechaAlta = c.FechaAlta, FechaBaja = c.FechaBaja } }
            };
            if (entidad.Notas is { Length: > 2000 }) entidad.Notas = entidad.Notas[..2000];
            foreach (var m in c.Modelos)
                entidad.Modelos.Add(NuevoModelo(m));

            nuevos[clave] = (c, entidad);
            resultado.Creados.Add(c);
        }

        if (aplicar && nuevos.Count > 0)
        {
            db.Clientes.AddRange(nuevos.Values.Select(v => v.Entidad));
            await db.SaveChangesAsync(ct);
        }
        return resultado;
    }

    private static ClienteModelo NuevoModelo(ModeloImportado m) => new()
    {
        ModeloCodigo = m.Codigo,
        FechaAlta = m.FechaAlta,
        FechaBaja = m.FechaBaja,
        Notas = m.Notas is { Length: > 500 } ? m.Notas[..500] : m.Notas
    };

    private static string Clave(string nombre, TipoCliente tipo) => $"{tipo}:{TextoBusqueda.Normalizar(nombre)}";
}
