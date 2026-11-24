using AgendaLogar.Core.Seguridad;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.Navegacion;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>CU-02 Cambiar contraseña. Es obligatorio si el usuario es nuevo o el administrador se la ha restablecido.</summary>
public partial class CambiarPasswordViewModel : ViewModelBase
{
    private readonly ServicioAutenticacion _autenticacion;
    private readonly INavegacionService _navegacion;

    public CambiarPasswordViewModel(ServicioAutenticacion autenticacion, INavegacionService navegacion, ISesion sesion)
    {
        _autenticacion = autenticacion;
        _navegacion = navegacion;
        Obligatorio = sesion.Usuario?.DebeCambiarPassword == true;
    }

    /// <summary>Si es true no se puede cancelar: hay que cambiarla para entrar.</summary>
    public bool Obligatorio { get; }

    public string Explicacion => Obligatorio
        ? "Por seguridad, elige una contraseña nueva antes de continuar."
        : "Introduce tu contraseña actual y la nueva dos veces.";

    public string Requisitos => ReglasPassword.Descripcion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private string _passwordActual = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private string _passwordNueva = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private string _passwordRepetida = string.Empty;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private string? _exito;

    private bool PuedeGuardar() => !Ocupado
        && PasswordActual.Length > 0 && PasswordNueva.Length > 0 && PasswordRepetida.Length > 0;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        Error = null;
        Exito = null;
        Ocupado = true;
        try
        {
            await _autenticacion.CambiarPasswordAsync(PasswordActual, PasswordNueva, PasswordRepetida);
            PasswordActual = PasswordNueva = PasswordRepetida = string.Empty;

            if (Obligatorio)
                _navegacion.NavegarA<InicioViewModel>();
            else
                Exito = "Contraseña cambiada correctamente.";
        }
        catch (Exception ex) when (ex is ReglaNegocioException or AccesoDenegadoException)
        {
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Error = $"No se ha podido cambiar la contraseña: {ex.GetBaseException().Message}";
        }
        finally
        {
            Ocupado = false;
            GuardarCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void Cancelar() => _navegacion.NavegarA<InicioViewModel>();

    [RelayCommand]
    private void CerrarSesion()
    {
        _autenticacion.CerrarSesion();
        _navegacion.NavegarA<LoginViewModel>();
    }
}
