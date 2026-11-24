namespace AgendaLogar.Core.Seguridad;

/// <summary>Requisitos mínimos de las contraseñas.</summary>
public static class ReglasPassword
{
    public const int LongitudMinima = 8;

    public const string Descripcion = "Mínimo 8 caracteres, con al menos una letra y un número.";

    /// <summary>Devuelve el motivo por el que la contraseña no es válida, o null si es válida.</summary>
    public static string? Validar(string? password, string? nombreUsuario = null)
    {
        if (string.IsNullOrEmpty(password) || password.Length < LongitudMinima)
            return $"La contraseña debe tener al menos {LongitudMinima} caracteres.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "La contraseña debe tener al menos una letra y un número.";
        if (!string.IsNullOrWhiteSpace(nombreUsuario)
            && password.Contains(nombreUsuario.Trim(), StringComparison.OrdinalIgnoreCase))
            return "La contraseña no puede contener el nombre de usuario.";
        return null;
    }
}
