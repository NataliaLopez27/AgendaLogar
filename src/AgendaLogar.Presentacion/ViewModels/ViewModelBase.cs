using CommunityToolkit.Mvvm.ComponentModel;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>Base de todos los ViewModels navegables.</summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _ocupado;

    [ObservableProperty]
    private string? _mensaje;

    /// <summary>Se llama al mostrar la vista. Útil para cargar datos.</summary>
    public virtual Task AlEntrarAsync() => Task.CompletedTask;

    /// <summary>Se llama al salir de la vista.</summary>
    public virtual void AlSalir() { }
}
