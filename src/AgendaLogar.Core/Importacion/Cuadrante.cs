using System.Text.RegularExpressions;
using AgendaLogar.Core.Dominio;
using ClosedXML.Excel;

namespace AgendaLogar.Core.Importacion;

/// <summary>Un modelo de un cliente leído del cuadrante.</summary>
public sealed record ModeloImportado(string Codigo, DateOnly FechaAlta, DateOnly? FechaBaja, string? Notas);

/// <summary>Un cliente leído de una fila del cuadrante.</summary>
public sealed class ClienteImportado
{
    public required string Origen { get; init; }
    public required int Fila { get; init; }
    public required string Nombre { get; init; }
    public required string Nif { get; init; }
    public required TipoCliente Tipo { get; init; }
    public required DateOnly FechaAlta { get; init; }
    public DateOnly? FechaBaja { get; init; }
    public List<ModeloImportado> Modelos { get; } = new();
    public List<string> Avisos { get; } = new();

    public bool NifValido => Nif.Length > 0 && ValidadorNif.EsValido(Nif);
    public string Ubicacion => $"{Origen}, fila {Fila}";
}

/// <summary>Resultado de leer un cuadrante.</summary>
public sealed class CuadranteLeido
{
    public List<ClienteImportado> Clientes { get; } = new();

    /// <summary>Problemas del archivo entero (por ejemplo, no se encuentra la cabecera).</summary>
    public List<string> Avisos { get; } = new();
}

/// <summary>Lee los cuadrantes Excel actuales (RF-60).</summary>
public static partial class Cuadrante
{
    [GeneratedRegex(@"(?<!\d)(\d{3})(?!\d)")]
    private static partial Regex CodigoModelo();

    /// <summary>Lee la hoja de un .xlsx como una tabla de textos (tal y como se ven en Excel).</summary>
    public static IReadOnlyList<IReadOnlyList<string>> LeerTabla(Stream xlsx, string? hoja = null)
    {
        using var libro = new XLWorkbook(xlsx);
        var ws = hoja is null ? libro.Worksheets.First() : libro.Worksheet(hoja);
        var rango = ws.RangeUsed();
        if (rango is null) return Array.Empty<IReadOnlyList<string>>();

        var ultimaFila = rango.LastRow().RowNumber();
        var ultimaColumna = rango.LastColumn().ColumnNumber();
        var filas = new List<IReadOnlyList<string>>();
        for (var f = 1; f <= ultimaFila; f++)
        {
            var fila = new string[ultimaColumna];
            for (var c = 1; c <= ultimaColumna; c++)
                fila[c - 1] = ws.Cell(f, c).GetFormattedString().Trim();
            filas.Add(fila);
        }
        return filas;
    }

    /// <summary>
    /// Interpreta la tabla de un cuadrante:
    /// busca la fila de cabecera (la que tiene códigos de modelo del catálogo), la columna del nombre
    /// y la del NIF, y convierte cada fila en un cliente con sus modelos.
    /// </summary>
    /// <param name="catalogo">Modelos del catálogo (para reconocer las columnas y saber a qué tipo aplican).</param>
    /// <param name="fechaAltaPorDefecto">Fecha de alta cuando el cuadrante no la indica.</param>
    public static CuadranteLeido Interpretar(IReadOnlyList<IReadOnlyList<string>> tabla, string origen, TipoCliente tipo,
        IReadOnlyCollection<Modelo> catalogo, DateOnly fechaAltaPorDefecto)
    {
        var resultado = new CuadranteLeido();
        var porCodigo = catalogo.ToDictionary(m => m.Codigo);

        // 1. Cabecera: la primera fila con al menos dos códigos de modelo del catálogo.
        var filaCabecera = -1;
        var columnasModelo = new Dictionary<int, string>();
        for (var f = 0; f < tabla.Count && filaCabecera < 0; f++)
        {
            var encontrados = new Dictionary<int, string>();
            for (var c = 0; c < tabla[f].Count; c++)
            {
                var m = CodigoModelo().Match(tabla[f][c]);
                if (m.Success && porCodigo.ContainsKey(m.Groups[1].Value) && !encontrados.ContainsValue(m.Groups[1].Value))
                    encontrados[c] = m.Groups[1].Value;
            }
            if (encontrados.Count >= 2)
            {
                filaCabecera = f;
                columnasModelo = encontrados;
            }
        }
        if (filaCabecera < 0)
        {
            resultado.Avisos.Add($"{origen}: no se ha encontrado la fila de cabecera con los códigos de modelo (303, 111…).");
            return resultado;
        }

        // 2. Columnas de nombre y NIF por su título; si no hay título de nombre, la primera columna con texto.
        var cabecera = tabla[filaCabecera];
        int? colNif = BuscarColumna(cabecera, columnasModelo, "NIF", "CIF", "DNI");
        int? colNombre = BuscarColumna(cabecera, columnasModelo, "NOMBRE", "CLIENTE", "RAZON", "RAZÓN", "APELLIDOS");
        colNombre ??= Enumerable.Range(0, cabecera.Count)
            .Where(c => !columnasModelo.ContainsKey(c) && c != colNif)
            .Cast<int?>()
            .FirstOrDefault(c => tabla.Skip(filaCabecera + 1).Any(fila => c < fila.Count && fila[c!.Value].Any(char.IsLetter)));
        if (colNombre is null)
        {
            resultado.Avisos.Add($"{origen}: no se ha encontrado la columna del nombre del cliente.");
            return resultado;
        }

        // 3. Una fila por cliente.
        for (var f = filaCabecera + 1; f < tabla.Count; f++)
        {
            var fila = tabla[f];
            string Celda(int c) => c < fila.Count ? fila[c] : string.Empty;

            var nombreCelda = Celda(colNombre.Value);
            if (!nombreCelda.Any(char.IsLetter)) continue; // fila vacía o de totales

            var anotado = Anotaciones.InterpretarNombre(nombreCelda);
            if (anotado.Nombre.Length == 0 || EsFilaDeTotales(anotado.Nombre)) continue;

            var nif = colNif is int cn ? ValidadorNif.Normalizar(Celda(cn)) : string.Empty;
            var cliente = new ClienteImportado
            {
                Origen = origen,
                Fila = f + 1,
                Nombre = anotado.Nombre,
                Nif = nif,
                Tipo = tipo,
                FechaAlta = anotado.FechaAlta ?? fechaAltaPorDefecto,
                FechaBaja = anotado.FechaBaja
            };
            if (anotado.Aviso is not null) cliente.Avisos.Add(anotado.Aviso);
            if (nif.Length == 0) cliente.Avisos.Add("Sin NIF: hay que completarlo a mano.");
            else if (!cliente.NifValido) cliente.Avisos.Add($"NIF «{nif}» no válido: revísalo.");
            if (cliente.FechaBaja < cliente.FechaAlta)
            {
                cliente.Avisos.Add($"La baja ({cliente.FechaBaja:dd/MM/yyyy}) es anterior al alta; se ignora la baja.");
                cliente = Copiar(cliente, fechaBaja: null);
            }

            foreach (var (columna, codigo) in columnasModelo)
            {
                var celda = Anotaciones.InterpretarCeldaModelo(Celda(columna));
                if (celda.Aviso is not null) cliente.Avisos.Add($"Modelo {codigo}: {celda.Aviso}");
                if (!celda.Lleva) continue;

                var modelo = porCodigo[codigo];
                if (!(tipo == TipoCliente.Fisica ? modelo.AplicaFisica : modelo.AplicaSociedad))
                {
                    cliente.Avisos.Add($"Modelo {codigo} marcado pero no se aplica a este tipo de cliente; no se importa.");
                    continue;
                }

                var alta = celda.FechaAlta ?? cliente.FechaAlta;
                if (alta < cliente.FechaAlta) alta = cliente.FechaAlta;
                var baja = celda.FechaBaja;
                if (baja < alta)
                {
                    cliente.Avisos.Add($"Modelo {codigo}: la baja es anterior al alta; se ignora la baja.");
                    baja = null;
                }
                cliente.Modelos.Add(new ModeloImportado(codigo, alta, baja, celda.Nota));
            }

            resultado.Clientes.Add(cliente);
        }

        return resultado;
    }

    private static readonly string[] Totales = { "total", "totales", "suma", "subtotal" };

    private static bool EsFilaDeTotales(string nombre) => Totales.Contains(TextoBusqueda.Normalizar(nombre).TrimEnd(':', '.'));

    private static int? BuscarColumna(IReadOnlyList<string> cabecera, Dictionary<int, string> columnasModelo, params string[] titulos)
    {
        for (var c = 0; c < cabecera.Count; c++)
        {
            if (columnasModelo.ContainsKey(c)) continue;
            var texto = cabecera[c].ToUpperInvariant();
            if (titulos.Any(t => texto.Contains(t))) return c;
        }
        return null;
    }

    private static ClienteImportado Copiar(ClienteImportado c, DateOnly? fechaBaja)
    {
        var copia = new ClienteImportado
        {
            Origen = c.Origen, Fila = c.Fila, Nombre = c.Nombre, Nif = c.Nif, Tipo = c.Tipo,
            FechaAlta = c.FechaAlta, FechaBaja = fechaBaja
        };
        copia.Avisos.AddRange(c.Avisos);
        copia.Modelos.AddRange(c.Modelos);
        return copia;
    }
}
