namespace AgendaLogar.Core.Seguridad;

/// <summary>Calcula y comprueba hashes de contraseñas.</summary>
public interface IHashPassword
{
    string Calcular(string password);
    bool Verificar(string password, string hash);
}

/// <summary>Implementación con BCrypt (RNF-03).</summary>
public class HashPasswordBCrypt : IHashPassword
{
    private readonly int _coste;

    public HashPasswordBCrypt(int coste = 11)
    {
        _coste = coste;
    }

    public string Calcular(string password) => BCrypt.Net.BCrypt.HashPassword(password, _coste);

    public bool Verificar(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
