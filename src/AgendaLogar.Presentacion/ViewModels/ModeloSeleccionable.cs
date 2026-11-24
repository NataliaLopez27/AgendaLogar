using CommunityToolkit.Mvvm.ComponentModel;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>Modelo fiscal en el formulario de alta de cliente: se marca y se le pone fecha de alta.</summary>
public partial class ModeloSeleccionable : ObservableObject
{
    public ModeloSeleccionable(string codigo, string descripcion, bool esAnual, DateTime? fechaAlta)
    {
        Codigo = codigo;
        Descripcion = descripcion;
        EsAnual = esAnual;
        _fechaAlta = fechaAlta;
    }

    public string Codigo { get; }
    public string Descripcion { get; }
    public bool EsAnual { get; }
    public string Periodicidad => EsAnual ? "Anual" : "Trimestral";

    [ObservableProperty]
    private bool _marcado;

    [ObservableProperty]
    private DateTime? _fechaAlta;
}
