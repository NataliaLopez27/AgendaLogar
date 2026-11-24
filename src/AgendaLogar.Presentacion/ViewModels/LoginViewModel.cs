using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.Navegacion;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>CU-01 Iniciar sesión.</summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly ServicioAutenticacion _autenticacion;
    private readonly INavegacionService _navegacion;

    public LoginViewModel(ServicioAutenticacion autenticacion, INavegacionService navegacion)
    {
        _autenticacion = autenticacion;
        _navegacion = navegacion;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IniciarSesionCommand))]
    private string _nombreUsuario = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IniciarSesionCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _error;

    private bool PuedeIniciarSesion() =>
        !Ocupado && !string.IsNullOrWhiteSpace(NombreUsuario) && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(PuedeIniciarSesion))]
    private async Task IniciarSesionAsync()
    {
        Error = null;
        Ocupado = true;
        try
        {
            var usuario = await _autenticacion.IniciarSesionAsync(NombreUsuario, Password);
            Password = string.Empty;

            if (usuario.DebeCambiarPassword)
                _navegacion.NavegarA<CambiarPasswordViewModel>();
            else
                _navegacion.NavegarA<InicioViewModel>();
        }
        catch (ReglaNegocioException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Error = $"No se ha podido iniciar sesión: {ex.GetBaseException().Message}";
        }
        finally
        {
            Ocupado = false;
            IniciarSesionCommand.NotifyCanExecuteChanged();
        }
    }
}
