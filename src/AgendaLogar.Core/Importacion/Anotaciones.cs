using System.Globalization;
using System.Text.RegularExpressions;

namespace AgendaLogar.Core.Importacion;

/// <summary>Lo que significa una celda de modelo del cuadrante.</summary>
public sealed record CeldaModelo(bool Lleva, DateOnly? FechaAlta, DateOnly? FechaBaja, string? Nota, string? Aviso);

/// <summary>Nombre del cliente sin las anotaciones de alta o baja que se escribían a mano.</summary>
public sealed record NombreAnotado(string Nombre, DateOnly? FechaAlta, DateOnly? FechaBaja, string? Aviso);

/// <summary>
/// Interpreta las anotaciones escritas a mano en los cuadrantes (sección 1.2 de los requisitos):
/// "X", "X(A22-7-24)", "( A 1-1-2026)", "BAJA 30-10-2026", "NO" y notas libres.
/// </summary>
public static partial class Anotaciones
{
    // Fecha d-m-aa o d-m-aaaa, con guiones, barras o puntos.
    private const string Fecha = @"(\d{1,2}\s*[-/.]\s*\d{1,2}\s*[-/.]\s*\d{2,4})";

    [GeneratedRegex(@"\(\s*A\s*\.?\s*" + Fecha + @"\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex AltaEntreParentesis();

    [GeneratedRegex(@"\bBAJA\b\s*:?\s*" + Fecha + @"?", RegexOptions.IgnoreCase)]
    private static partial Regex Baja();

    [GeneratedRegex(@"^\s*X\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmpiezaPorX();

    [GeneratedRegex(@"^\s*NO\b\s*[.!]*\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SoloNo();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Espacios();

    /// <summary>Convierte "1-1-2026", "22-7-24" o "30/10/2026" en fecha. Los años de dos cifras son 20xx.</summary>
    public static DateOnly? ParsearFecha(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var partes = Regex.Split(texto.Trim(), @"\s*[-/.]\s*");
        if (partes.Length != 3
            || !int.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out var d)
            || !int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            || !int.TryParse(partes[2], NumberStyles.None, CultureInfo.InvariantCulture, out var a))
            return null;
        if (partes[2].Length == 2) a += 2000;
        if (a < 1900 || m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(a, m)) return null;
        return new DateOnly(a, m, d);
    }

    /// <summary>Separa el nombre de las anotaciones "( A 1-1-2026)" y "BAJA 30-10-2026".</summary>
    public static NombreAnotado InterpretarNombre(string? celda)
    {
        var texto = celda ?? string.Empty;
        string? aviso = null;
        DateOnly? alta = null, baja = null;

        var mAlta = AltaEntreParentesis().Match(texto);
        if (mAlta.Success)
        {
            alta = ParsearFecha(mAlta.Groups[1].Value);
            if (alta is null) aviso = $"Fecha de alta no válida: «{mAlta.Value.Trim()}».";
            texto = texto.Remove(mAlta.Index, mAlta.Length);
        }

        var mBaja = Baja().Match(texto);
        if (mBaja.Success)
        {
            baja = ParsearFecha(mBaja.Groups[1].Value);
            if (baja is null) aviso = Unir(aviso, $"Baja sin fecha válida: «{mBaja.Value.Trim()}».");
            texto = texto.Remove(mBaja.Index, mBaja.Length);
        }

        var nombre = Espacios().Replace(texto, " ").Trim().Trim('-', ',', ';', '(', ')').Trim();
        return new NombreAnotado(nombre, alta, baja, aviso);
    }

    /// <summary>
    /// Interpreta una celda de modelo:
    /// vacía o "NO" → no lo lleva; "X" → lo lleva; "X(A22-7-24)" → lo lleva desde esa fecha;
    /// "BAJA 30-10-2026" → lo llevaba hasta esa fecha; cualquier otro texto → lo lleva, con el texto como nota (a revisar).
    /// </summary>
    public static CeldaModelo InterpretarCeldaModelo(string? celda)
    {
        var texto = celda?.Trim() ?? string.Empty;
        if (texto.Length == 0) return new CeldaModelo(false, null, null, null, null);
        if (SoloNo().IsMatch(texto)) return new CeldaModelo(false, null, null, null, null);

        string? aviso = null;
        DateOnly? alta = null, baja = null;
        var resto = texto;

        var tieneX = EmpiezaPorX().IsMatch(resto);
        if (tieneX) resto = EmpiezaPorX().Replace(resto, string.Empty, 1);

        var mAlta = AltaEntreParentesis().Match(resto);
        if (mAlta.Success)
        {
            alta = ParsearFecha(mAlta.Groups[1].Value);
            if (alta is null) aviso = $"Fecha de alta no válida: «{mAlta.Value.Trim()}».";
            resto = resto.Remove(mAlta.Index, mAlta.Length);
        }

        var mBaja = Baja().Match(resto);
        if (mBaja.Success)
        {
            baja = ParsearFecha(mBaja.Groups[1].Value);
            if (baja is null) aviso = Unir(aviso, $"Baja sin fecha válida: «{mBaja.Value.Trim()}».");
            resto = resto.Remove(mBaja.Index, mBaja.Length);
        }

        var nota = Espacios().Replace(resto, " ").Trim().Trim('-', ',', ';', '(', ')').Trim();
        if (nota.Length == 0) nota = null;

        // Texto libre sin "X" ni fechas (por ejemplo "arrendamiento"): se entiende que lo lleva, pero hay que revisarlo.
        if (!tieneX && !mAlta.Success && !mBaja.Success)
            aviso = Unir(aviso, $"Texto «{texto}» interpretado como que lleva el modelo; revísalo.");

        return new CeldaModelo(true, alta, baja, nota, aviso);
    }

    private static string? Unir(string? a, string b) => a is null ? b : $"{a} {b}";
}
