using AgendaLogar.Presentacion.Navegacion;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>ViewModel de la ventana principal: menú lateral y vista actual.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly INavegacionService _navegacion;

    [ObservableProperty]
    private ViewModelBase? _vistaActual;

    public MainViewModel(INavegacionService navegacion)
    {
        _navegacion = navegacion;
        _navegacion.VistaCambiada += (_, _) => VistaActual = _navegacion.VistaActual;
    }

    public string Titulo => "AgendaLogar";

    [RelayCommand]
    private void IrAInicio() => _navegacion.NavegarA<InicioViewModel>();

    [RelayCommand]
    private void IrACatalogoModelos() => _navegacion.NavegarA<CatalogoModelosViewModel>();
}
