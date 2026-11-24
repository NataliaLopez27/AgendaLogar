using System.Collections.ObjectModel;
using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Dominio;
using AgendaLogar.Presentacion.Navegacion;
using Microsoft.EntityFrameworkCore;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>Muestra el catálogo de modelos fiscales cargado por el seed.</summary>
[SoloAdministrador]
public class CatalogoModelosViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AgendaLogarDbContext> _fabrica;

    public CatalogoModelosViewModel(IDbContextFactory<AgendaLogarDbContext> fabrica)
    {
        _fabrica = fabrica;
    }

    public ObservableCollection<Modelo> Modelos { get; } = new();

    public override async Task AlEntrarAsync()
    {
        Ocupado = true;
        Mensaje = "Cargando modelos…";
        try
        {
            await using var db = await _fabrica.CreateDbContextAsync();
            var modelos = await db.Modelos.AsNoTracking().OrderBy(m => m.Orden).ToListAsync();
            Modelos.Clear();
            foreach (var m in modelos)
                Modelos.Add(m);
            Mensaje = $"{Modelos.Count} modelos en el catálogo.";
        }
        catch (Exception ex)
        {
            Mensaje = $"Error al cargar los modelos: {ex.GetBaseException().Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }
}
