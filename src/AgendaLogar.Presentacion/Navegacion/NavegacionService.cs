using System.Reflection;
using AgendaLogar.Core.Seguridad;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.Presentacion.Navegacion;

/// <summary>Cambia la vista que se muestra en la ventana principal.</summary>
public interface INavegacionService
{
    ViewModelBase? VistaActual { get; }
    event EventHandler? VistaCambiada;
    void NavegarA<TViewModel>() where TViewModel : ViewModelBase;
}

/// <summary>
/// Navegación con control de acceso: sin sesión solo se abren las vistas públicas
/// y las vistas de administración solo las abre el administrador.
/// </summary>
public class NavegacionService : INavegacionService
{
    private readonly Func<Type, ViewModelBase> _fabrica;
    private readonly ISesion _sesion;

    public NavegacionService(Func<Type, ViewModelBase> fabrica, ISesion sesion)
    {
        _fabrica = fabrica;
        _sesion = sesion;
    }

    public ViewModelBase? VistaActual { get; private set; }
    public event EventHandler? VistaCambiada;

    public void NavegarA<TViewModel>() where TViewModel : ViewModelBase => Navegar(DestinoPermitido(typeof(TViewModel)));

    /// <summary>Devuelve el destino pedido o, si no está permitido, el acceso o el inicio.</summary>
    public Type DestinoPermitido(Type destino)
    {
        if (destino.GetCustomAttribute<VistaPublicaAttribute>() is not null)
            return destino;
        if (!_sesion.Iniciada)
            return typeof(LoginViewModel);
        if (destino.GetCustomAttribute<SoloAdministradorAttribute>() is not null && !_sesion.Usuario!.EsAdmin)
            return typeof(InicioViewModel);
        return destino;
    }

    private void Navegar(Type tipo)
    {
        var nueva = _fabrica(tipo);
        VistaActual?.AlSalir();
        VistaActual = nueva;
        VistaCambiada?.Invoke(this, EventArgs.Empty);
        _ = nueva.AlEntrarAsync();
    }
}
