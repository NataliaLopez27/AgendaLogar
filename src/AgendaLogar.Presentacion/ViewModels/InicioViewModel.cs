using AgendaLogar.Core.Seguridad;
using AgendaLogar.Presentacion.Navegacion;
using CommunityToolkit.Mvvm.Input;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>
/// Pantalla de inicio tras iniciar sesión, con accesos directos.
/// En el hito 4 mostrará "Mis asignaciones · Trimestral" (CU-01, paso 3).
/// </summary>
public partial class InicioViewModel : ViewModelBase
{
    private readonly INavegacionService _navegacion;

    public InicioViewModel(ISesion sesion, INavegacionService navegacion)
    {
        _navegacion = navegacion;
        var usuario = sesion.Usuario;
        Saludo = usuario is null ? "Hola" : $"Hola, {usuario.Nombre}";
        EsAdmin = usuario?.EsAdmin == true;
        Rol = EsAdmin ? "Administrador" : "Empleado";
        Fecha = DateTime.Today.ToString("dddd, d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-ES"));
        Fecha = char.ToUpper(Fecha[0]) + Fecha[1..];
    }

    public string Saludo { get; }
    public string Rol { get; }
    public bool EsAdmin { get; }
    public string Fecha { get; }

    [RelayCommand]
    private void IrACambiarPassword() => _navegacion.NavegarA<CambiarPasswordViewModel>();

    [RelayCommand]
    private void IrAClientes() => _navegacion.NavegarA<ClientesViewModel>();

    [RelayCommand]
    private void IrAEmpleados() => _navegacion.NavegarA<EmpleadosViewModel>();

    [RelayCommand]
    private void IrACatalogoModelos() => _navegacion.NavegarA<CatalogoModelosViewModel>();
}
