using AgendaLogar.Core.Seguridad;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.Navegacion;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>ViewModel de la ventana principal: menú lateral, usuario conectado y vista actual.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly INavegacionService _navegacion;
    private readonly ISesion _sesion;
    private readonly ServicioAutenticacion _autenticacion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeccionActual))]
    [NotifyPropertyChangedFor(nameof(MostrarMenu))]
    private ViewModelBase? _vistaActual;

    public MainViewModel(INavegacionService navegacion, ISesion sesion, ServicioAutenticacion autenticacion)
    {
        _navegacion = navegacion;
        _sesion = sesion;
        _autenticacion = autenticacion;
        _navegacion.VistaCambiada += (_, _) => VistaActual = _navegacion.VistaActual;
        _sesion.Cambiada += (_, _) => AlCambiarSesion();
    }

    public string Titulo => "AgendaLogar · Logar Asesores";

    /// <summary>Nombre del tipo de la vista actual, para resaltar la opción del menú.</summary>
    public string? SeccionActual => VistaActual?.GetType().Name;

    /// <summary>El menú solo se ve con sesión iniciada y fuera del cambio obligatorio de contraseña.</summary>
    public bool MostrarMenu => _sesion.Iniciada
        && _sesion.Usuario!.DebeCambiarPassword == false;

    public string NombreUsuario => _sesion.Usuario?.Nombre ?? string.Empty;
    public string RolUsuario => _sesion.Usuario?.EsAdmin == true ? "Administrador" : "Empleado";
    public string Iniciales => ObtenerIniciales(_sesion.Usuario?.Nombre);
    public bool EsAdmin => _sesion.Usuario?.EsAdmin == true;

    [RelayCommand]
    private void IrAInicio() => _navegacion.NavegarA<InicioViewModel>();

    [RelayCommand]
    private void IrACatalogoModelos() => _navegacion.NavegarA<CatalogoModelosViewModel>();

    [RelayCommand]
    private void IrACambiarPassword() => _navegacion.NavegarA<CambiarPasswordViewModel>();

    /// <summary>CU-01: cerrar sesión y volver a la pantalla de acceso.</summary>
    [RelayCommand]
    private void CerrarSesion()
    {
        _autenticacion.CerrarSesion();
        _navegacion.NavegarA<LoginViewModel>();
    }

    private void AlCambiarSesion()
    {
        OnPropertyChanged(nameof(MostrarMenu));
        OnPropertyChanged(nameof(NombreUsuario));
        OnPropertyChanged(nameof(RolUsuario));
        OnPropertyChanged(nameof(Iniciales));
        OnPropertyChanged(nameof(EsAdmin));
    }

    private static string ObtenerIniciales(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return string.Empty;
        var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(partes.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }
}
