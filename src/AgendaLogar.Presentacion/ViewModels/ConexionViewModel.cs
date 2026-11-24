using AgendaLogar.Core.Datos;
using AgendaLogar.Presentacion.Navegacion;
using CommunityToolkit.Mvvm.Input;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>
/// Primera pantalla: conecta con la base de datos, aplica las migraciones y crea el administrador inicial.
/// Muestra "Conectando…" mientras Azure SQL se despierta (RNF-03c).
/// </summary>
[VistaPublica]
public partial class ConexionViewModel : ViewModelBase
{
    private readonly InicializadorBaseDatos _inicializador;
    private readonly OpcionesBaseDatos _opciones;
    private readonly INavegacionService _navegacion;

    public ConexionViewModel(InicializadorBaseDatos inicializador, OpcionesBaseDatos opciones, INavegacionService navegacion)
    {
        _inicializador = inicializador;
        _opciones = opciones;
        _navegacion = navegacion;
    }

    public bool HayError { get => _hayError; private set => SetProperty(ref _hayError, value); }
    private bool _hayError;

    public override Task AlEntrarAsync() => ConectarAsync();

    [RelayCommand]
    private async Task ConectarAsync()
    {
        Ocupado = true;
        HayError = false;
        Mensaje = "Conectando…";
        try
        {
            await _inicializador.InicializarAsync(_opciones.AplicarMigracionesAlIniciar);
            _navegacion.NavegarA<LoginViewModel>();
        }
        catch (Exception ex)
        {
            HayError = true;
            Mensaje = $"No se ha podido conectar con la base de datos. {ex.GetBaseException().Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }
}
