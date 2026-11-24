using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.Presentacion.Navegacion;

/// <summary>Cambia la vista que se muestra en la ventana principal.</summary>
public interface INavegacionService
{
    ViewModelBase? VistaActual { get; }
    event EventHandler? VistaCambiada;
    void NavegarA<TViewModel>() where TViewModel : ViewModelBase;
}

public class NavegacionService : INavegacionService
{
    private readonly Func<Type, ViewModelBase> _fabrica;

    public NavegacionService(Func<Type, ViewModelBase> fabrica)
    {
        _fabrica = fabrica;
    }

    public ViewModelBase? VistaActual { get; private set; }
    public event EventHandler? VistaCambiada;

    public void NavegarA<TViewModel>() where TViewModel : ViewModelBase
    {
        var nueva = _fabrica(typeof(TViewModel));
        VistaActual?.AlSalir();
        VistaActual = nueva;
        VistaCambiada?.Invoke(this, EventArgs.Empty);
        _ = nueva.AlEntrarAsync();
    }
}
