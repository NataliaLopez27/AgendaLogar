namespace AgendaLogar.Core.Servicios;

/// <summary>Error de negocio con un mensaje que se puede enseñar al usuario tal cual.</summary>
public class ReglaNegocioException : Exception
{
    public ReglaNegocioException(string mensaje) : base(mensaje) { }
}

/// <summary>El usuario actual no tiene permiso para la operación (RNF-04b).</summary>
public class AccesoDenegadoException : Exception
{
    public AccesoDenegadoException(string mensaje = "No tienes permiso para realizar esta operación.") : base(mensaje) { }
}
