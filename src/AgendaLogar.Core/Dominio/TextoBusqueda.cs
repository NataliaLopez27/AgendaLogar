using System.Globalization;
using System.Text;

namespace AgendaLogar.Core.Dominio;

/// <summary>Prepara textos para buscar sin distinguir mayúsculas ni acentos ("López" = "lopez").</summary>
public static class TextoBusqueda
{
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            // Se quitan las tildes y diéresis, pero se conserva la ñ.
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark
                && !(c == '̃' && sb.Length > 0 && (sb[^1] == 'n' || sb[^1] == 'N')))
                continue;
            sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
