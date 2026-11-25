using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Core.Servicios;

/// <summary>Fila de la lista de periodos, con su progreso.</summary>
public sealed record PeriodoResumen(int Id, int Anio, byte? Trimestre, EstadoPeriodo Estado,
    int Total, int SinAsignar, int Pendientes, int Hechos, int Listos)
{
    public string Nombre => Trimestre is byte t ? $"{t}T {Anio}" : $"Anual {Anio}";
    public bool EsAnual => Trimestre is null;
    public bool Abierto => Estado == EstadoPeriodo.Abierto;
    public string EstadoTexto => Abierto ? "Abierto" : "Cerrado";
    public string Progreso => Total == 0 ? "Sin clientes" : $"{Listos} de {Total} listos para mandar";
    public double Porcentaje => Total == 0 ? 0 : 100.0 * Listos / Total;
    public string SinAsignarTexto => SinAsignar == 0 ? "Todos asignados" : $"{SinAsignar} sin asignar";
}

/// <summary>Resultado de crear un periodo (CU-15, paso 4).</summary>
public sealed record ResultadoCreacionPeriodo(int PeriodoId, string Nombre, int Total, int Asignados, int SinAsignar,
    IReadOnlyList<string> BajasDesdeAnterior, string? PeriodoAnterior)
{
    public string Resumen
    {
        get
        {
            var texto = $"Se ha creado el {Nombre} con {Total} clientes: {Asignados} asignados y {SinAsignar} sin asignar.";
            if (PeriodoAnterior is not null && BajasDesdeAnterior.Count > 0)
                texto += $" {BajasDesdeAnterior.Count} cliente(s) del {PeriodoAnterior} ya no están vigentes.";
            return texto;
        }
    }
}

/// <summary>Fila de la tabla de asignaciones de un periodo.</summary>
public sealed record AsignacionFila(int Id, int ClienteId, string Cliente, string Nif, TipoCliente Tipo,
    string Modelos, int? UsuarioId, string Empleado, EstadoAsignacion Estado, decimal? Importe, DateOnly? BajaEnPeriodo)
{
    public string TipoTexto => Tipo == TipoCliente.Fisica ? "Persona física" : "Sociedad";
    public bool SinAsignar => UsuarioId is null;

    /// <summary>RN-05: "Baja dd/mm/aaaa" si es su último periodo.</summary>
    public string? EtiquetaBaja => BajaEnPeriodo is DateOnly b ? $"Baja {b:dd/MM/yyyy}" : null;

    public string EstadoTexto => Estado switch
    {
        EstadoAsignacion.Hecho => "Hecho",
        EstadoAsignacion.ListoParaMandar => "Listo para mandar",
        _ => "Pendiente"
    };
}

/// <summary>Filtros de la tabla de asignaciones.</summary>
public sealed record FiltroAsignaciones(string? Texto = null, TipoCliente? Tipo = null, int? EmpleadoId = null, bool SoloSinAsignar = false);

/// <summary>Periodos y asignaciones (CU-15, RF-41, CU-16, CU-17). Todas las operaciones son solo para el administrador.</summary>
public class ServicioPeriodos
{
    private readonly IDbContextFactory<AgendaLogarDbContext> _fabrica;
    private readonly ControlAcceso _acceso;

    public ServicioPeriodos(IDbContextFactory<AgendaLogarDbContext> fabrica, ControlAcceso acceso)
    {
        _fabrica = fabrica;
        _acceso = acceso;
    }

    /// <summary>Periodos del más reciente al más antiguo (en cada año, el anual después del 4T).</summary>
    public async Task<IReadOnlyList<PeriodoResumen>> ListarAsync(CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var periodos = await db.Periodos.AsNoTracking()
            .Select(p => new
            {
                p.Id, p.Anio, p.Trimestre, p.Estado,
                Total = p.Asignaciones.Count(),
                SinAsignar = p.Asignaciones.Count(a => a.UsuarioId == null),
                Pendientes = p.Asignaciones.Count(a => a.Estado == EstadoAsignacion.Pendiente),
                Hechos = p.Asignaciones.Count(a => a.Estado == EstadoAsignacion.Hecho),
                Listos = p.Asignaciones.Count(a => a.Estado == EstadoAsignacion.ListoParaMandar)
            })
            .ToListAsync(ct);

        return periodos
            .OrderByDescending(p => p.Anio).ThenByDescending(p => p.Trimestre ?? 5)
            .Select(p => new PeriodoResumen(p.Id, p.Anio, p.Trimestre, p.Estado, p.Total, p.SinAsignar, p.Pendientes, p.Hechos, p.Listos))
            .ToList();
    }

    /// <summary>
    /// CU-15: crea un periodo y una asignación <i>Pendiente</i> por cada cliente vigente (RN-01, RF-44).
    /// RF-41: si <paramref name="copiarAnterior"/>, los clientes que estaban en el periodo anterior del mismo tipo
    /// conservan su empleado (si sigue activo); los demás quedan sin asignar.
    /// </summary>
    public async Task<ResultadoCreacionPeriodo> CrearAsync(int anio, byte? trimestre, bool copiarAnterior, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        if (anio is < 2000 or > 2100)
            throw new ReglaNegocioException("El año no es válido.");
        if (trimestre is not null and not (>= 1 and <= 4))
            throw new ReglaNegocioException("El trimestre tiene que ser del 1 al 4.");

        var periodo = new Periodo { Anio = anio, Trimestre = trimestre, Estado = EstadoPeriodo.Abierto };
        if (await db.Periodos.AnyAsync(p => p.Anio == anio && p.Trimestre == trimestre, ct))
            throw new ReglaNegocioException($"El periodo {periodo.Nombre} ya existe.");

        var catalogo = await db.Modelos.AsNoTracking().ToDictionaryAsync(m => m.Codigo, ct);
        var clientes = await db.Clientes.AsNoTracking().Include(c => c.Altas).Include(c => c.Modelos).ToListAsync(ct);
        var vigentes = clientes.Where(c => ReglasPeriodo.EntraEnPeriodo(c, periodo, catalogo)).ToList();

        // Periodo anterior del mismo tipo (RF-41)
        var (anioAnt, trimAnt) = ReglasPeriodo.Anterior(anio, trimestre);
        var anterior = await db.Periodos.AsNoTracking()
            .Where(p => p.Anio == anioAnt && p.Trimestre == trimAnt)
            .Select(p => new
            {
                p.Anio,
                p.Trimestre,
                Asignaciones = p.Asignaciones.Select(a => new { a.ClienteId, a.UsuarioId, Activo = a.Usuario != null && a.Usuario.Activo })
            })
            .FirstOrDefaultAsync(ct);

        var empleadoAnterior = new Dictionary<int, int>();
        if (copiarAnterior && anterior is not null)
            foreach (var a in anterior.Asignaciones.Where(a => a.UsuarioId is not null && a.Activo))
                empleadoAnterior[a.ClienteId] = a.UsuarioId!.Value;

        foreach (var c in vigentes)
            periodo.Asignaciones.Add(new Asignacion
            {
                ClienteId = c.Id,
                UsuarioId = empleadoAnterior.TryGetValue(c.Id, out var u) ? u : null,
                Estado = EstadoAsignacion.Pendiente
            });

        db.Periodos.Add(periodo);
        await db.SaveChangesAsync(ct);

        var idsVigentes = vigentes.Select(c => c.Id).ToHashSet();
        var bajas = anterior is null
            ? new List<string>()
            : anterior.Asignaciones.Where(a => !idsVigentes.Contains(a.ClienteId))
                .Join(clientes, a => a.ClienteId, c => c.Id, (a, c) => c.Nombre)
                .OrderBy(n => n).ToList();

        var asignados = periodo.Asignaciones.Count(a => a.UsuarioId is not null);
        return new ResultadoCreacionPeriodo(periodo.Id, periodo.Nombre, periodo.Asignaciones.Count, asignados,
            periodo.Asignaciones.Count - asignados, bajas,
            anterior is null ? null : new Periodo { Anio = anterior.Anio, Trimestre = anterior.Trimestre }.Nombre);
    }

    /// <summary>Asignaciones del periodo con sus modelos vigentes (RN-02) y la etiqueta de baja (RN-05).</summary>
    public async Task<IReadOnlyList<AsignacionFila>> AsignacionesAsync(int periodoId, FiltroAsignaciones? filtro = null, CancellationToken ct = default)
    {
        filtro ??= new FiltroAsignaciones();
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var periodo = await db.Periodos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == periodoId, ct)
            ?? throw new ReglaNegocioException("El periodo no existe.");
        var catalogo = await db.Modelos.AsNoTracking().ToDictionaryAsync(m => m.Codigo, ct);

        var asignaciones = await db.Asignaciones.AsNoTracking()
            .Where(a => a.PeriodoId == periodoId)
            .Include(a => a.Cliente).ThenInclude(c => c.Altas)
            .Include(a => a.Cliente).ThenInclude(c => c.Modelos)
            .Include(a => a.Usuario)
            .AsSplitQuery()
            .ToListAsync(ct);

        var texto = TextoBusqueda.Normalizar(filtro.Texto);
        var nif = ValidadorNif.Normalizar(filtro.Texto);

        return asignaciones
            .Select(a => new AsignacionFila(a.Id, a.ClienteId, a.Cliente.Nombre, a.Cliente.Nif, a.Cliente.Tipo,
                string.Join(", ", ReglasPeriodo.ModelosVigentes(a.Cliente, periodo, catalogo)),
                a.UsuarioId, a.Usuario?.Nombre ?? ClienteResumen.SinAsignar, a.Estado, a.Importe,
                ReglasPeriodo.BajaDentroDelPeriodo(a.Cliente, periodo)))
            .Where(f => texto.Length == 0 || TextoBusqueda.Normalizar(f.Cliente).Contains(texto) || (nif.Length > 0 && f.Nif.Contains(nif)))
            .Where(f => filtro.Tipo is not TipoCliente t || f.Tipo == t)
            .Where(f => filtro.SoloSinAsignar ? f.SinAsignar : filtro.EmpleadoId is not int e || f.UsuarioId == e)
            .OrderBy(f => TextoBusqueda.Normalizar(f.Cliente), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Usuarios activos a los que se puede asignar clientes (el administrador también, decisión 5).</summary>
    public async Task<IReadOnlyList<EmpleadoFiltro>> EmpleadosAsignablesAsync(CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);
        return await db.Usuarios.AsNoTracking().Where(u => u.Activo).OrderBy(u => u.Nombre)
            .Select(u => new EmpleadoFiltro(u.Id, u.Nombre)).ToListAsync(ct);
    }

    /// <summary>
    /// CU-16: asigna (o reasigna) una o varias asignaciones del periodo a un empleado.
    /// Con <paramref name="usuarioId"/> nulo se quitan (quedan sin asignar). Solo en periodos abiertos.
    /// </summary>
    public async Task<int> AsignarAsync(int periodoId, IReadOnlyCollection<int> asignacionIds, int? usuarioId, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var admin = await _acceso.RequerirAdminAsync(db, ct);

        if (asignacionIds.Count == 0)
            throw new ReglaNegocioException("Selecciona al menos un cliente.");
        var periodo = await db.Periodos.SingleOrDefaultAsync(p => p.Id == periodoId, ct)
            ?? throw new ReglaNegocioException("El periodo no existe.");
        if (periodo.Estado != EstadoPeriodo.Abierto)
            throw new ReglaNegocioException($"El {periodo.Nombre} está cerrado: reábrelo para cambiar las asignaciones.");

        if (usuarioId is int id && !await db.Usuarios.AnyAsync(u => u.Id == id && u.Activo, ct))
            throw new ReglaNegocioException("El empleado no existe o está desactivado.");

        var asignaciones = await db.Asignaciones
            .Where(a => a.PeriodoId == periodoId && asignacionIds.Contains(a.Id))
            .ToListAsync(ct);
        if (asignaciones.Count != asignacionIds.Distinct().Count())
            throw new ReglaNegocioException("Alguno de los clientes no pertenece a este periodo.");

        var ahora = DateTime.Now;
        foreach (var a in asignaciones.Where(a => a.UsuarioId != usuarioId))
        {
            db.HistorialAsignaciones.Add(new HistorialAsignacion
            {
                AsignacionId = a.Id, Campo = "Empleado",
                ValorAnterior = a.UsuarioId?.ToString(), ValorNuevo = usuarioId?.ToString(),
                UsuarioId = admin.Id, Fecha = ahora
            });
            a.UsuarioId = usuarioId;
            a.ModificadoPorId = admin.Id;
            a.ModificadoEn = ahora;
        }
        await db.SaveChangesAsync(ct);
        return asignaciones.Count;
    }

    /// <summary>CU-17: cierra el periodo (solo lectura para los empleados).</summary>
    public async Task CerrarAsync(int periodoId, CancellationToken ct = default) =>
        await CambiarEstadoAsync(periodoId, EstadoPeriodo.Cerrado, ct);

    /// <summary>CU-17: reabre un periodo cerrado.</summary>
    public async Task ReabrirAsync(int periodoId, CancellationToken ct = default) =>
        await CambiarEstadoAsync(periodoId, EstadoPeriodo.Abierto, ct);

    private async Task CambiarEstadoAsync(int periodoId, EstadoPeriodo estado, CancellationToken ct)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);
        var periodo = await db.Periodos.SingleOrDefaultAsync(p => p.Id == periodoId, ct)
            ?? throw new ReglaNegocioException("El periodo no existe.");
        if (periodo.Estado == estado)
            throw new ReglaNegocioException($"El {periodo.Nombre} ya está {(estado == EstadoPeriodo.Abierto ? "abierto" : "cerrado")}.");
        periodo.Estado = estado;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// RN-04: periodos abiertos ya creados en los que el cliente es vigente pero todavía no está
    /// (por ejemplo, tras darlo de alta o reactivarlo con una fecha dentro de ellos).
    /// </summary>
    public async Task<IReadOnlyList<PeriodoResumen>> PeriodosAbiertosSinClienteAsync(int clienteId, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var cliente = await db.Clientes.AsNoTracking().Include(c => c.Altas).Include(c => c.Modelos)
            .SingleOrDefaultAsync(c => c.Id == clienteId, ct);
        if (cliente is null) return Array.Empty<PeriodoResumen>();
        var catalogo = await db.Modelos.AsNoTracking().ToDictionaryAsync(m => m.Codigo, ct);

        var abiertos = await db.Periodos.AsNoTracking()
            .Where(p => p.Estado == EstadoPeriodo.Abierto && !p.Asignaciones.Any(a => a.ClienteId == clienteId))
            .ToListAsync(ct);
        return abiertos
            .Where(p => ReglasPeriodo.EntraEnPeriodo(cliente, p, catalogo))
            .OrderBy(p => p.Anio).ThenBy(p => p.Trimestre ?? 5)
            .Select(p => new PeriodoResumen(p.Id, p.Anio, p.Trimestre, p.Estado, 0, 0, 0, 0, 0))
            .ToList();
    }

    /// <summary>RN-04: añade el cliente a esos periodos (pendiente y sin asignar, o con el empleado indicado).</summary>
    public async Task AnadirClienteAPeriodosAsync(int clienteId, IReadOnlyCollection<int> periodoIds, int? usuarioId = null, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        foreach (var pid in periodoIds)
        {
            var periodo = await db.Periodos.SingleOrDefaultAsync(p => p.Id == pid, ct)
                ?? throw new ReglaNegocioException("El periodo no existe.");
            if (periodo.Estado != EstadoPeriodo.Abierto)
                throw new ReglaNegocioException($"El {periodo.Nombre} está cerrado.");
            if (await db.Asignaciones.AnyAsync(a => a.PeriodoId == pid && a.ClienteId == clienteId, ct))
                continue;
            db.Asignaciones.Add(new Asignacion { PeriodoId = pid, ClienteId = clienteId, UsuarioId = usuarioId });
        }
        await db.SaveChangesAsync(ct);
    }
}
