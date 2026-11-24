namespace AgendaLogar.Core.Datos;

/// <summary>Opciones de la sección "BaseDatos" de appsettings.json.</summary>
public class OpcionesBaseDatos
{
    /// <summary>Aplica las migraciones pendientes al arrancar. Útil en desarrollo (LocalDB).</summary>
    public bool AplicarMigracionesAlIniciar { get; set; }
}
