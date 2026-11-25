using System.Globalization;
using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Importacion;
using Microsoft.EntityFrameworkCore;

// RF-60 · Importación de clientes desde los cuadrantes Excel.
//
// Uso:
//   dotnet run --project tools/AgendaLogar.Importador -- <archivo.xlsx | carpeta>... --tipo fisica|sociedad [opciones]
//
// Opciones:
//   --tipo fisica|sociedad   Tipo de los clientes del cuadrante. Si no se indica, se deduce del nombre del archivo
//                            ("SOCIEDADES" → sociedad; "FISICAS"/"PERSONAS" → física).
//   --hoja <nombre>          Hoja del libro (por defecto, la primera).
//   --fecha-alta <d-m-aaaa>  Fecha de alta cuando el cuadrante no la indica (por defecto 1-1-2000).
//   --conexion "<cadena>"    Base de datos (por defecto AGENDALOGAR_CONNECTION o LocalDB).
//   --aplicar                Guarda los cambios. Sin esta opción solo se simula y se muestra el informe.

var argumentos = new Argumentos(args);
if (argumentos.Rutas.Count == 0 || argumentos.Ayuda)
{
    Console.WriteLine("""
        Importación de clientes desde los cuadrantes Excel (RF-60)

        Uso:
          dotnet run --project tools/AgendaLogar.Importador -- <archivo.xlsx | carpeta>... [opciones]

        Opciones:
          --tipo fisica|sociedad   Tipo de los clientes (si no, se deduce del nombre del archivo).
          --hoja <nombre>          Hoja del libro (por defecto, la primera).
          --fecha-alta <d-m-aaaa>  Fecha de alta cuando el cuadrante no la indica (por defecto 1-1-2000).
          --conexion "<cadena>"    Base de datos (por defecto AGENDALOGAR_CONNECTION o LocalDB).
          --aplicar                Guarda los cambios. Sin esta opción solo se simula.
        """);
    return argumentos.Ayuda ? 0 : 1;
}

var fechaAlta = argumentos.FechaAlta is null ? new DateOnly(2000, 1, 1) : Anotaciones.ParsearFecha(argumentos.FechaAlta);
if (fechaAlta is null)
{
    Console.Error.WriteLine($"Fecha no válida: {argumentos.FechaAlta}");
    return 1;
}

var cadena = argumentos.Conexion
    ?? Environment.GetEnvironmentVariable("AGENDALOGAR_CONNECTION")
    ?? DesignTimeDbContextFactory.CadenaLocalDb;
var opciones = new DbContextOptionsBuilder<AgendaLogarDbContext>()
    .UseSqlServer(cadena, sql => sql.EnableRetryOnFailure())
    .Options;
var fabrica = new FabricaContexto(opciones);

await using (var db = fabrica.CreateDbContext())
{
    Console.WriteLine("Conectando con la base de datos…");
    await db.Database.MigrateAsync();
}

List<Modelo> catalogo;
await using (var db = fabrica.CreateDbContext())
    catalogo = await db.Modelos.AsNoTracking().ToListAsync();

// Archivos a leer
var archivos = argumentos.Rutas
    .SelectMany(r => Directory.Exists(r)
        ? Directory.GetFiles(r, "*.xlsx").Where(f => !Path.GetFileName(f).StartsWith("~$"))
        : new[] { r })
    .ToList();

var leidos = new List<ClienteImportado>();
var huboErrores = false;
foreach (var archivo in archivos)
{
    var nombre = Path.GetFileName(archivo);
    var tipo = argumentos.Tipo ?? DeducirTipo(nombre);
    if (tipo is null)
    {
        Console.Error.WriteLine($"✗ {nombre}: indica --tipo fisica|sociedad (no se puede deducir del nombre).");
        huboErrores = true;
        continue;
    }

    try
    {
        await using var stream = File.OpenRead(archivo);
        var tabla = Cuadrante.LeerTabla(stream, argumentos.Hoja);
        var cuadrante = Cuadrante.Interpretar(tabla, nombre, tipo.Value, catalogo, fechaAlta.Value);
        foreach (var aviso in cuadrante.Avisos) Console.Error.WriteLine($"✗ {aviso}");
        Console.WriteLine($"• {nombre}: {cuadrante.Clientes.Count} clientes ({(tipo == TipoCliente.Fisica ? "personas físicas" : "sociedades")}).");
        leidos.AddRange(cuadrante.Clientes);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"✗ {nombre}: no se ha podido leer ({ex.GetBaseException().Message}).");
        huboErrores = true;
    }
}

var importador = new ImportadorClientes(fabrica);
var resultado = await importador.ImportarAsync(leidos, argumentos.Aplicar);

// Informe
Console.WriteLine();
Console.WriteLine(argumentos.Aplicar ? "=== IMPORTACIÓN REALIZADA ===" : "=== SIMULACIÓN (no se ha guardado nada; usa --aplicar) ===");
Console.WriteLine($"Clientes nuevos: {resultado.Creados.Count}");
Console.WriteLine($"Omitidos:        {resultado.Omitidos.Count}");

var conAvisos = resultado.Creados.Where(c => c.Avisos.Count > 0).ToList();
if (conAvisos.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"--- Para revisar a mano ({conAvisos.Count}) ---");
    foreach (var c in conAvisos)
    {
        Console.WriteLine($"{c.Nombre}  [{c.Ubicacion}]");
        foreach (var a in c.Avisos) Console.WriteLine($"    - {a}");
    }
}

if (resultado.Omitidos.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("--- Omitidos ---");
    foreach (var (c, motivo) in resultado.Omitidos)
        Console.WriteLine($"{c.Nombre}  [{c.Ubicacion}]: {motivo}");
}

return huboErrores ? 2 : 0;

static TipoCliente? DeducirTipo(string nombreArchivo)
{
    var n = TextoBusqueda.Normalizar(nombreArchivo);
    if (n.Contains("sociedad")) return TipoCliente.Sociedad;
    if (n.Contains("fisica") || n.Contains("persona")) return TipoCliente.Fisica;
    return null;
}

/// <summary>Fábrica de contextos para la importación.</summary>
sealed class FabricaContexto(DbContextOptions<AgendaLogarDbContext> opciones) : IDbContextFactory<AgendaLogarDbContext>
{
    public AgendaLogarDbContext CreateDbContext() => new(opciones);
}

/// <summary>Lectura sencilla de los argumentos de la línea de comandos.</summary>
sealed class Argumentos
{
    public List<string> Rutas { get; } = new();
    public TipoCliente? Tipo { get; }
    public string? Hoja { get; }
    public string? FechaAlta { get; }
    public string? Conexion { get; }
    public bool Aplicar { get; }
    public bool Ayuda { get; }

    public Argumentos(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            string Siguiente() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Falta el valor de {args[i]}");
            switch (args[i].ToLower(CultureInfo.InvariantCulture))
            {
                case "--tipo":
                    var t = TextoBusqueda.Normalizar(Siguiente());
                    Tipo = t.StartsWith("soc") ? TipoCliente.Sociedad : t.StartsWith("fis") ? TipoCliente.Fisica
                        : throw new ArgumentException("--tipo debe ser fisica o sociedad");
                    break;
                case "--hoja": Hoja = Siguiente(); break;
                case "--fecha-alta": FechaAlta = Siguiente(); break;
                case "--conexion": Conexion = Siguiente(); break;
                case "--aplicar": Aplicar = true; break;
                case "-h" or "--help" or "--ayuda": Ayuda = true; break;
                default: Rutas.Add(args[i]); break;
            }
        }
    }
}
