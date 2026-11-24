using AgendaLogar.Core.Seguridad;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>
/// Pantalla de inicio tras iniciar sesión. En el hito 4 será "Mis asignaciones · Trimestral" (CU-01, paso 3).
/// </summary>
public class InicioViewModel : ViewModelBase
{
    public InicioViewModel(ISesion sesion)
    {
        var usuario = sesion.Usuario;
        Saludo = usuario is null ? "Hola" : $"Hola, {usuario.Nombre}";
        Rol = usuario?.EsAdmin == true ? "Administrador" : "Empleado";
    }

    public string Saludo { get; }
    public string Rol { get; }
}
