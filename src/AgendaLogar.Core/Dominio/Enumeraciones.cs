namespace AgendaLogar.Core.Dominio;

/// <summary>Rol de un usuario de la aplicación (RF-02).</summary>
public enum Rol
{
    Empleado = 0,
    Admin = 1
}

/// <summary>Tipo de cliente: persona física o sociedad.</summary>
public enum TipoCliente
{
    Fisica = 0,
    Sociedad = 1
}

/// <summary>Periodicidad de un modelo fiscal (sección 3).</summary>
public enum Periodicidad
{
    Trimestral = 0,
    Anual = 1
}

/// <summary>Estado de un periodo (RF-45).</summary>
public enum EstadoPeriodo
{
    Abierto = 0,
    Cerrado = 1
}

/// <summary>Estado de un cliente dentro de un periodo (RF-15b). Solo existen estos tres.</summary>
public enum EstadoAsignacion
{
    Pendiente = 0,
    Hecho = 1,
    ListoParaMandar = 2
}
