using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Core.Servicios;

/// <summary>Fila del listado de clientes.</summary>
public sealed record ClienteResumen(
    int Id, string Nombre, string Nif, TipoCliente Tipo,
    DateOnly? FechaAlta, DateOnly? FechaBaja, string Modelos)
{
    /// <summary>Tiene un tramo de alta abierto (sin fecha de baja).</summary>
    public bool Activo => FechaAlta is not null && FechaBaja is null;

    public string TipoTexto => Tipo == TipoCliente.Fisica ? "Persona física" : "Sociedad";

    public string EstadoTexto => Activo ? "Activo"
        : FechaBaja is DateOnly b ? $"Baja {b:dd/MM/yyyy}"
        : "Sin alta";

    /// <summary>RF-25: empleado que lo llevó en el último periodo en que estuvo asignado (null = sin asignar).</summary>
    public int? EmpleadoHabitualId { get; init; }

    public string EmpleadoHabitual { get; init; } = SinAsignar;

    public const string SinAsignar = "Sin asignar";
}

/// <summary>Estado por el que se filtra el listado de clientes.</summary>
public enum FiltroEstadoCliente
{
    Todos,
    Activos,
    DeBaja
}

/// <summary>
/// Filtros del listado de clientes (RF-25).
/// <see cref="EmpleadoId"/>: null = cualquiera, <see cref="SoloSinAsignar"/> = sin empleado habitual.
/// </summary>
public sealed record FiltroClientes(
    string? Texto = null,
    TipoCliente? Tipo = null,
    FiltroEstadoCliente Estado = FiltroEstadoCliente.Todos,
    int? EmpleadoId = null,
    bool SoloSinAsignar = false);

/// <summary>Empleado que se puede elegir en el filtro de empleado habitual.</summary>
public sealed record EmpleadoFiltro(int Id, string Nombre);

/// <summary>Un tramo del historial de altas y bajas de un cliente.</summary>
public sealed record TramoAlta(DateOnly FechaAlta, DateOnly? FechaBaja)
{
    public string Texto => FechaBaja is DateOnly b
        ? $"Alta {FechaAlta:dd/MM/yyyy} · Baja {b:dd/MM/yyyy}"
        : $"Alta {FechaAlta:dd/MM/yyyy} · Sigue de alta";
}

/// <summary>Un tramo de un modelo del cliente (CU-14). Si no tiene fecha de baja, está vigente.</summary>
public sealed record TramoModelo(int Id, string Codigo, string Descripcion, Periodicidad Periodicidad,
    DateOnly FechaAlta, DateOnly? FechaBaja, string? Notas)
{
    public bool Vigente => FechaBaja is null;
    public string PeriodicidadTexto => Periodicidad == Periodicidad.Anual ? "Anual" : "Trimestral";
    public string EstadoTexto => FechaBaja is DateOnly b ? $"Baja {b:dd/MM/yyyy}" : "Vigente";
}

/// <summary>Datos básicos de un cliente (alta y edición).</summary>
public sealed record DatosCliente(string Nombre, string Nif, TipoCliente Tipo, string? Notas);

/// <summary>Modelo marcado al dar de alta un cliente, con su fecha de alta.</summary>
public sealed record ModeloAlta(string Codigo, DateOnly FechaAlta);

/// <summary>Gestión de clientes (CU-11 a CU-14 y RF-25). Todas las operaciones son solo para el administrador.</summary>
public class ServicioClientes
{
    private readonly IDbContextFactory<AgendaLogarDbContext> _fabrica;
    private readonly ControlAcceso _acceso;

    public ServicioClientes(IDbContextFactory<AgendaLogarDbContext> fabrica, ControlAcceso acceso)
    {
        _fabrica = fabrica;
        _acceso = acceso;
    }

    /// <summary>Atajo del listado con los filtros más usados.</summary>
    public Task<IReadOnlyList<ClienteResumen>> ListarAsync(
        string? texto = null, TipoCliente? tipo = null, bool incluirBajas = true, CancellationToken ct = default) =>
        ListarAsync(new FiltroClientes(texto, tipo, incluirBajas ? FiltroEstadoCliente.Todos : FiltroEstadoCliente.Activos), ct);

    /// <summary>
    /// RF-25: listado de clientes con búsqueda por nombre o NIF (sin distinguir mayúsculas ni acentos)
    /// y filtros por tipo, estado y empleado habitual.
    /// Con unos 400 clientes, la búsqueda sin acentos se hace en memoria: así funciona igual en cualquier base de datos.
    /// </summary>
    public async Task<IReadOnlyList<ClienteResumen>> ListarAsync(FiltroClientes filtro, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var consulta = db.Clientes.AsNoTracking().Include(c => c.Altas).Include(c => c.Modelos).AsQueryable();
        if (filtro.Tipo is TipoCliente t)
            consulta = consulta.Where(c => c.Tipo == t);
        var clientes = await consulta.ToListAsync(ct);

        // Empleado habitual: el del último periodo en que el cliente estuvo asignado.
        var asignaciones = await db.Asignaciones.AsNoTracking()
            .Where(a => a.UsuarioId != null)
            .Select(a => new { a.ClienteId, a.Periodo.Anio, a.Periodo.Trimestre, a.Id, a.UsuarioId, a.Usuario!.Nombre })
            .ToListAsync(ct);
        var habituales = asignaciones
            .GroupBy(a => a.ClienteId)
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(a => a.Anio).ThenByDescending(a => a.Trimestre ?? 4).ThenByDescending(a => a.Id)
                .First());

        var texto = TextoBusqueda.Normalizar(filtro.Texto);
        var nif = ValidadorNif.Normalizar(filtro.Texto);

        return clientes
            .Select(c =>
            {
                var r = Resumen(c);
                return habituales.TryGetValue(c.Id, out var h)
                    ? r with { EmpleadoHabitualId = h.UsuarioId, EmpleadoHabitual = h.Nombre }
                    : r;
            })
            .Where(r => texto.Length == 0
                || TextoBusqueda.Normalizar(r.Nombre).Contains(texto)
                || (nif.Length > 0 && r.Nif.Contains(nif)))
            .Where(r => filtro.Estado switch
            {
                FiltroEstadoCliente.Activos => r.Activo,
                FiltroEstadoCliente.DeBaja => !r.Activo,
                _ => true
            })
            .Where(r => filtro.SoloSinAsignar
                ? r.EmpleadoHabitualId is null
                : filtro.EmpleadoId is not int e || r.EmpleadoHabitualId == e)
            .OrderBy(r => TextoBusqueda.Normalizar(r.Nombre), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Usuarios que se pueden elegir en el filtro de empleado habitual.</summary>
    public async Task<IReadOnlyList<EmpleadoFiltro>> EmpleadosParaFiltroAsync(CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);
        return await db.Usuarios.AsNoTracking()
            .OrderByDescending(u => u.Activo).ThenBy(u => u.Nombre)
            .Select(u => new EmpleadoFiltro(u.Id, u.Nombre))
            .ToListAsync(ct);
    }

    /// <summary>Modelos del catálogo que aplican a un tipo de cliente, en orden.</summary>
    public async Task<IReadOnlyList<Modelo>> ModelosParaAsync(TipoCliente tipo, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        return await db.Modelos.AsNoTracking()
            .Where(m => tipo == TipoCliente.Fisica ? m.AplicaFisica : m.AplicaSociedad)
            .OrderBy(m => m.Orden)
            .ToListAsync(ct);
    }

    /// <summary>CU-11 alternativo: devuelve otro cliente con el mismo NIF, para avisar (no bloquea).</summary>
    public async Task<ClienteResumen?> BuscarNifDuplicadoAsync(string nif, int? excluirId = null, CancellationToken ct = default)
    {
        var normalizado = ValidadorNif.Normalizar(nif);
        if (normalizado.Length == 0) return null;

        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);
        var cliente = await db.Clientes.AsNoTracking().Include(c => c.Altas).Include(c => c.Modelos)
            .FirstOrDefaultAsync(c => c.Nif == normalizado && c.Id != excluirId, ct);
        return cliente is null ? null : Resumen(cliente);
    }

    /// <summary>
    /// CU-11: da de alta un cliente con su fecha de alta y los modelos que lleva.
    /// El NIF duplicado no se comprueba aquí: la pantalla avisa antes con <see cref="BuscarNifDuplicadoAsync"/>.
    /// </summary>
    public async Task<ClienteResumen> CrearAsync(DatosCliente datos, DateOnly fechaAlta,
        IEnumerable<ModeloAlta> modelos, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var (nombre, nif, notas) = Validar(datos);
        var lista = modelos.ToList();

        var codigos = lista.Select(m => m.Codigo).ToList();
        if (codigos.Distinct().Count() != codigos.Count)
            throw new ReglaNegocioException("Hay modelos repetidos.");

        var catalogo = await db.Modelos.Where(m => codigos.Contains(m.Codigo)).ToListAsync(ct);
        foreach (var m in lista)
        {
            var modelo = catalogo.SingleOrDefault(c => c.Codigo == m.Codigo)
                ?? throw new ReglaNegocioException($"El modelo {m.Codigo} no existe.");
            if (!(datos.Tipo == TipoCliente.Fisica ? modelo.AplicaFisica : modelo.AplicaSociedad))
                throw new ReglaNegocioException($"El modelo {m.Codigo} no se aplica a {TipoTexto(datos.Tipo)}.");
            if (m.FechaAlta < fechaAlta)
                throw new ReglaNegocioException(
                    $"La fecha de alta del modelo {m.Codigo} no puede ser anterior a la del cliente ({fechaAlta:dd/MM/yyyy}).");
        }

        var cliente = new Cliente
        {
            Nombre = nombre,
            Nif = nif,
            Tipo = datos.Tipo,
            Notas = notas,
            Altas = { new AltaCliente { FechaAlta = fechaAlta } }
        };
        foreach (var m in lista)
            cliente.Modelos.Add(new ClienteModelo { ModeloCodigo = m.Codigo, FechaAlta = m.FechaAlta });

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync(ct);
        return Resumen(cliente);
    }

    /// <summary>Datos básicos de un cliente, para rellenar el formulario de edición.</summary>
    public async Task<DatosCliente> ObtenerDatosAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);
        var c = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ReglaNegocioException("El cliente no existe.");
        return new DatosCliente(c.Nombre, c.Nif, c.Tipo, c.Notas);
    }

    /// <summary>
    /// CU-12: modifica nombre, NIF, tipo y notas. Las fechas de alta y baja se gestionan en CU-13.
    /// No deja cambiar el tipo si el cliente lleva modelos (sin baja) que no aplican al nuevo tipo.
    /// </summary>
    public async Task<ClienteResumen> EditarAsync(int id, DatosCliente datos, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var cliente = await db.Clientes.Include(c => c.Altas).Include(c => c.Modelos).ThenInclude(m => m.Modelo)
            .SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new ReglaNegocioException("El cliente no existe.");
        var (nombre, nif, notas) = Validar(datos);

        if (datos.Tipo != cliente.Tipo)
        {
            var incompatibles = cliente.Modelos
                .Where(m => m.FechaBaja is null)
                .Where(m => !(datos.Tipo == TipoCliente.Fisica ? m.Modelo.AplicaFisica : m.Modelo.AplicaSociedad))
                .Select(m => m.ModeloCodigo)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            if (incompatibles.Count > 0)
                throw new ReglaNegocioException(
                    $"No se puede cambiar a {TipoTexto(datos.Tipo)}: lleva modelos que no aplican ({string.Join(", ", incompatibles)}). " +
                    "Dalos de baja antes en sus modelos.");
        }

        cliente.Nombre = nombre;
        cliente.Nif = nif;
        cliente.Tipo = datos.Tipo;
        cliente.Notas = notas;
        await db.SaveChangesAsync(ct);
        return Resumen(cliente);
    }

    /// <summary>CU-13: historial de altas y bajas, del más reciente al más antiguo.</summary>
    public async Task<IReadOnlyList<TramoAlta>> HistorialAltasAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);
        return await db.AltasCliente.AsNoTracking()
            .Where(a => a.ClienteId == id)
            .OrderByDescending(a => a.FechaAlta)
            .Select(a => new TramoAlta(a.FechaAlta, a.FechaBaja))
            .ToListAsync(ct);
    }

    /// <summary>
    /// CU-13 / RF-22: da de baja al cliente cerrando su tramo de alta actual. El cliente no se borra.
    /// Sigue apareciendo en el periodo que contiene la fecha de baja (RN-03) y no en los siguientes (RN-01).
    /// </summary>
    public async Task<ClienteResumen> DarDeBajaAsync(int id, DateOnly fechaBaja, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);
        var cliente = await CargarConHistorialAsync(db, id, ct);

        var abierto = cliente.Altas.SingleOrDefault(a => a.FechaBaja is null)
            ?? throw new ReglaNegocioException($"{cliente.Nombre} ya está dado de baja.");
        if (fechaBaja < abierto.FechaAlta)
            throw new ReglaNegocioException(
                $"La fecha de baja no puede ser anterior a la fecha de alta ({abierto.FechaAlta:dd/MM/yyyy}).");

        abierto.FechaBaja = fechaBaja;
        await db.SaveChangesAsync(ct);
        return Resumen(cliente);
    }

    /// <summary>CU-13 / RF-23: reactiva al cliente abriendo un tramo de alta nuevo. Se conserva el historial.</summary>
    public async Task<ClienteResumen> ReactivarAsync(int id, DateOnly fechaAlta, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);
        var cliente = await CargarConHistorialAsync(db, id, ct);

        if (cliente.Altas.Any(a => a.FechaBaja is null))
            throw new ReglaNegocioException($"{cliente.Nombre} ya está de alta.");
        var ultimaBaja = cliente.Altas.Max(a => a.FechaBaja);
        if (ultimaBaja is DateOnly b && fechaAlta <= b)
            throw new ReglaNegocioException(
                $"La nueva fecha de alta tiene que ser posterior a la última baja ({b:dd/MM/yyyy}).");

        cliente.Altas.Add(new AltaCliente { FechaAlta = fechaAlta });
        await db.SaveChangesAsync(ct);
        return Resumen(cliente);
    }

    // ---------------- CU-14: modelos del cliente ----------------

    /// <summary>Todos los tramos de modelos del cliente, en el orden del catálogo y del más reciente al más antiguo.</summary>
    public async Task<IReadOnlyList<TramoModelo>> ModelosDeClienteAsync(int clienteId, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);
        return await db.ClienteModelos.AsNoTracking()
            .Where(m => m.ClienteId == clienteId)
            .OrderBy(m => m.Modelo.Orden).ThenByDescending(m => m.FechaAlta)
            .Select(m => new TramoModelo(m.Id, m.ModeloCodigo, m.Modelo.Descripcion, m.Modelo.Periodicidad,
                m.FechaAlta, m.FechaBaja, m.Notas))
            .ToListAsync(ct);
    }

    /// <summary>
    /// CU-14: da de alta un modelo al cliente (por ejemplo, "alta en 115 el 17-07-2026").
    /// Si el modelo ya lo llevó y se dio de baja, la nueva alta tiene que ser posterior a esa baja.
    /// </summary>
    public async Task<TramoModelo> AnadirModeloAsync(int clienteId, string codigo, DateOnly fechaAlta,
        string? notas = null, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var cliente = await db.Clientes.Include(c => c.Modelos).SingleOrDefaultAsync(c => c.Id == clienteId, ct)
            ?? throw new ReglaNegocioException("El cliente no existe.");
        var modelo = await db.Modelos.SingleOrDefaultAsync(m => m.Codigo == codigo, ct)
            ?? throw new ReglaNegocioException($"El modelo {codigo} no existe.");
        if (!(cliente.Tipo == TipoCliente.Fisica ? modelo.AplicaFisica : modelo.AplicaSociedad))
            throw new ReglaNegocioException($"El modelo {codigo} no se aplica a {TipoTexto(cliente.Tipo)}.");

        var tramos = cliente.Modelos.Where(m => m.ModeloCodigo == codigo).ToList();
        if (tramos.Any(t => t.FechaBaja is null))
            throw new ReglaNegocioException($"{cliente.Nombre} ya lleva el modelo {codigo}.");
        var ultimaBaja = tramos.Max(t => t.FechaBaja);
        if (ultimaBaja is DateOnly b && fechaAlta <= b)
            throw new ReglaNegocioException(
                $"La fecha de alta del {codigo} tiene que ser posterior a su última baja ({b:dd/MM/yyyy}).");

        var tramo = new ClienteModelo
        {
            ModeloCodigo = codigo,
            FechaAlta = fechaAlta,
            Notas = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim()
        };
        cliente.Modelos.Add(tramo);
        await db.SaveChangesAsync(ct);
        return new TramoModelo(tramo.Id, codigo, modelo.Descripcion, modelo.Periodicidad, tramo.FechaAlta, null, tramo.Notas);
    }

    /// <summary>CU-14: da de baja un modelo del cliente (por ejemplo, "baja IVA 31-3-25").</summary>
    public async Task DarDeBajaModeloAsync(int tramoId, DateOnly fechaBaja, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var tramo = await db.ClienteModelos.SingleOrDefaultAsync(m => m.Id == tramoId, ct)
            ?? throw new ReglaNegocioException("El modelo no existe.");
        if (tramo.FechaBaja is not null)
            throw new ReglaNegocioException($"El modelo {tramo.ModeloCodigo} ya está dado de baja.");
        if (fechaBaja < tramo.FechaAlta)
            throw new ReglaNegocioException(
                $"La fecha de baja no puede ser anterior a la de alta del modelo ({tramo.FechaAlta:dd/MM/yyyy}).");

        tramo.FechaBaja = fechaBaja;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>CU-14: notas de un modelo del cliente (por ejemplo, "OJO cambiar número de cuenta").</summary>
    public async Task CambiarNotasModeloAsync(int tramoId, string? notas, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var tramo = await db.ClienteModelos.SingleOrDefaultAsync(m => m.Id == tramoId, ct)
            ?? throw new ReglaNegocioException("El modelo no existe.");
        var texto = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim();
        if (texto is { Length: > 500 })
            throw new ReglaNegocioException("Las notas del modelo no pueden tener más de 500 caracteres.");

        tramo.Notas = texto;
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Cliente> CargarConHistorialAsync(AgendaLogarDbContext db, int id, CancellationToken ct) =>
        await db.Clientes.Include(c => c.Altas).Include(c => c.Modelos).SingleOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new ReglaNegocioException("El cliente no existe.");

    private static (string nombre, string nif, string? notas) Validar(DatosCliente datos)
    {
        var nombre = datos.Nombre?.Trim() ?? string.Empty;
        var nif = ValidadorNif.Normalizar(datos.Nif);
        var notas = string.IsNullOrWhiteSpace(datos.Notas) ? null : datos.Notas.Trim();

        if (nombre.Length == 0)
            throw new ReglaNegocioException("Indica el nombre o la razón social.");
        if (nombre.Length > 200)
            throw new ReglaNegocioException("El nombre no puede tener más de 200 caracteres.");
        if (nif.Length == 0)
            throw new ReglaNegocioException("Indica el NIF/CIF.");
        if (!ValidadorNif.EsValido(nif))
            throw new ReglaNegocioException($"El NIF/CIF {nif} no es válido.");
        if (!Enum.IsDefined(datos.Tipo))
            throw new ReglaNegocioException("El tipo de cliente no es válido.");
        if (notas is { Length: > 2000 })
            throw new ReglaNegocioException("Las notas no pueden tener más de 2000 caracteres.");

        return (nombre, nif, notas);
    }

    private static string TipoTexto(TipoCliente tipo) => tipo == TipoCliente.Fisica ? "personas físicas" : "sociedades";

    private static ClienteResumen Resumen(Cliente c)
    {
        var ultima = c.Altas.OrderByDescending(a => a.FechaAlta).FirstOrDefault();
        var modelos = string.Join(", ", c.Modelos
            .Where(m => m.FechaBaja is null)
            .Select(m => m.ModeloCodigo)
            .Distinct()
            .OrderBy(x => x));
        return new ClienteResumen(c.Id, c.Nombre, c.Nif, c.Tipo, ultima?.FechaAlta, ultima?.FechaBaja, modelos);
    }
}
