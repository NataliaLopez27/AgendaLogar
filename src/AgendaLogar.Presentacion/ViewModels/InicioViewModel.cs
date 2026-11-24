using AgendaLogar.Core.Datos;
using CommunityToolkit.Mvvm.Input;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>Pantalla inicial: conecta con la base de datos y aplica las migraciones (RNF-03c).</summary>
public partial class InicioViewModel : ViewModelBase
{
    private readonly InicializadorBaseDatos _inicializador;
    private readonly bool _aplicarMigraciones;

    public InicioViewModel(InicializadorBaseDatos inicializador, OpcionesBaseDatos opciones)
    {
        _inicializador = inicializador;
        _aplicarMigraciones = opciones.AplicarMigracionesAlIniciar;
    }

    public override Task AlEntrarAsync() => ConectarAsync();

    [RelayCommand]
    private async Task ConectarAsync()
    {
        Ocupado = true;
        Mensaje = "Conectando…";
        try
        {
            await _inicializador.InicializarAsync(_aplicarMigraciones);
            Mensaje = "Conectado a la base de datos.";
        }
        catch (Exception ex)
        {
            Mensaje = $"No se ha podido conectar con la base de datos: {ex.GetBaseException().Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }
}
