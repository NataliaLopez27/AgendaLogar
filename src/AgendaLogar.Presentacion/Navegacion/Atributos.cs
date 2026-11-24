namespace AgendaLogar.Presentacion.Navegacion;

/// <summary>La vista se puede abrir sin haber iniciado sesión (conexión y acceso).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class VistaPublicaAttribute : Attribute;

/// <summary>Solo el administrador puede abrir la vista.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SoloAdministradorAttribute : Attribute;
