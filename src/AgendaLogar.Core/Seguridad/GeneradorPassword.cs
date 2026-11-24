using System.Security.Cryptography;

namespace AgendaLogar.Core.Seguridad;

/// <summary>Genera contraseñas temporales fáciles de dictar (sin 0/O ni 1/l/I).</summary>
public static class GeneradorPassword
{
    private const string Letras = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Numeros = "23456789";

    public static string Generar(int longitud = 10)
    {
        if (longitud < ReglasPassword.LongitudMinima)
            longitud = ReglasPassword.LongitudMinima;

        var todos = Letras + Numeros;
        var caracteres = new char[longitud];
        caracteres[0] = Letras[RandomNumberGenerator.GetInt32(Letras.Length)];
        caracteres[1] = Numeros[RandomNumberGenerator.GetInt32(Numeros.Length)];
        for (var i = 2; i < longitud; i++)
            caracteres[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];

        RandomNumberGenerator.Shuffle(caracteres.AsSpan());
        return new string(caracteres);
    }
}
