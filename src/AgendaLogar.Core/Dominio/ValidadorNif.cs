namespace AgendaLogar.Core.Dominio;

/// <summary>Validación del formato y la letra o dígito de control de DNI, NIE y CIF.</summary>
public static class ValidadorNif
{
    private const string LetrasDni = "TRWAGMYFPDXBNJZSQVHLCKE";
    private const string LetrasControlCif = "JABCDEFGHI";
    private const string LetrasInicialesCif = "ABCDEFGHJNPQRSUVW";

    /// <summary>Quita espacios, guiones y puntos, y pasa a mayúsculas.</summary>
    public static string Normalizar(string? nif) =>
        new string((nif ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>Indica si el NIF (ya normalizado o no) es un DNI, NIE o CIF válido.</summary>
    public static bool EsValido(string? nif)
    {
        var n = Normalizar(nif);
        if (n.Length != 9) return false;
        return EsDni(n) || EsNie(n) || EsCif(n);
    }

    private static bool EsDni(string n) =>
        n[..8].All(char.IsAsciiDigit) && n[8] == LetrasDni[int.Parse(n[..8]) % 23];

    private static bool EsNie(string n)
    {
        var prefijo = n[0] switch { 'X' => "0", 'Y' => "1", 'Z' => "2", _ => null };
        if (prefijo is null || !n[1..8].All(char.IsAsciiDigit)) return false;
        return n[8] == LetrasDni[int.Parse(prefijo + n[1..8]) % 23];
    }

    private static bool EsCif(string n)
    {
        if (!LetrasInicialesCif.Contains(n[0]) || !n[1..8].All(char.IsAsciiDigit)) return false;

        var suma = 0;
        for (var i = 0; i < 7; i++)
        {
            var d = n[i + 1] - '0';
            if (i % 2 == 0)
            {
                d *= 2;
                d = d / 10 + d % 10;
            }
            suma += d;
        }
        var control = (10 - suma % 10) % 10;
        var digito = (char)('0' + control);
        var letra = LetrasControlCif[control];

        return n[0] switch
        {
            'P' or 'Q' or 'R' or 'S' or 'N' or 'W' => n[8] == letra,
            'A' or 'B' or 'E' or 'H' => n[8] == digito,
            _ => n[8] == digito || n[8] == letra
        };
    }
}
