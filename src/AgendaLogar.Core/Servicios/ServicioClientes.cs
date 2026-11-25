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
}

/// <summary>Datos básicos de un cliente (alta y edición).</summary>
public sealed record DatosCliente(string Nombre, string Nif, TipoCliente Tipo, string? Notas);

/// <summary>Modelo marcado al dar de alta un cliente, con su fecha de alta.</summary>
public sealed record ModeloAlta(string Codigo, DateOnly FechaAlta);

/// <summary>Gestión de clientes (CU-11 a CU-13). Todas las operaciones son solo para el administrador.</summary>
public class ServicioClientes
{
    private readonly IDbContextFactory<AgendaLogarDbContext> _fabrica;
    private readonly ControlAcceso _acceso;

    public ServicioClientes(IDbContextFactory<AgendaLogarDbContext> fabrica, ControlAcceso acceso)
    {
        _fabrica = fabrica;
        _acceso = acceso;
    }

    /// <summary>RF-25 (básico): listado con búsqueda por nombre o NIF y filtro por tipo.</summary>
    public async Task<IReadOnlyList<ClienteResumen>> ListarAsync(
        string? texto = null, TipoCliente? tipo = null, bool incluirBajas = true, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        await _acceso.RequerirAdminAsync(db, ct);

        var consulta = db.Clientes.AsNoTracking().Include(c => c.Altas).Include(c => c.Modelos).AsQueryable();
        if (tipo is TipoCliente t)
            consulta = consulta.Where(c => c.Tipo == t);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var buscado = texto.Trim().ToLower();
            var nif = ValidadorNif.Normalizar(texto);
            consulta = consulta.Where(c => c.Nombre.ToLower().Contains(buscado) || (nif.Length > 0 && c.Nif.Contains(nif)));
        }

        var clientes = await consulta.OrderBy(c => c.Nombre).ToListAsync(ct);
        var resumenes = clientes.Select(Resumen);
        if (!incluirBajas)
            resumenes = resumenes.Where(r => r.Activo);
        return resumenes.ToList();
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
