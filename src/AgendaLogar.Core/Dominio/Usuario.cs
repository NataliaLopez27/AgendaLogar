namespace AgendaLogar.Core.Dominio;

/// <summary>Empleado o administrador que usa la aplicación.</summary>
public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Nombre con el que inicia sesión. Único.</summary>
    public string NombreUsuario { get; set; } = string.Empty;

    /// <summary>Hash BCrypt de la contraseña (RNF-03).</summary>
    public string HashPassword { get; set; } = string.Empty;

    public Rol Rol { get; set; } = Rol.Empleado;
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Obliga a cambiar la contraseña en el próximo inicio de sesión
    /// (usuario nuevo o contraseña restablecida por el administrador).
    /// </summary>
    public bool DebeCambiarPassword { get; set; }

    public ICollection<Asignacion> Asignaciones { get; set; } = new List<Asignacion>();
}
