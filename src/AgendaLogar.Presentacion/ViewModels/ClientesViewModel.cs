using System.Collections.ObjectModel;
using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.Navegacion;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>Qué está haciendo el panel lateral de la pantalla de clientes.</summary>
public enum ModoPanelCliente
{
    Ninguno,
    Alta,
    Edicion,
    Baja,
    Reactivacion
}

/// <summary>Opción de los desplegables de tipo de cliente. Valor nulo = todos.</summary>
public sealed record OpcionTipo(TipoCliente? Valor, string Texto);

/// <summary>Pantalla de clientes del administrador: listado, alta (CU-11) y edición (CU-12).</summary>
[SoloAdministrador]
public partial class ClientesViewModel : ViewModelBase
{
    private readonly ServicioClientes _clientes;
    private readonly IDialogos _dialogos;
    private CancellationTokenSource? _busqueda;

    public ClientesViewModel(ServicioClientes clientes, IDialogos dialogos)
    {
        _clientes = clientes;
        _dialogos = dialogos;
    }

    public ObservableCollection<ClienteResumen> Clientes { get; } = new();

    public static IReadOnlyList<OpcionTipo> FiltrosTipo { get; } = new[]
    {
        new OpcionTipo(null, "Todos"),
        new OpcionTipo(TipoCliente.Fisica, "Personas físicas"),
        new OpcionTipo(TipoCliente.Sociedad, "Sociedades")
    };

    public IReadOnlyList<OpcionTipo> Tipos { get; } = new[]
    {
        new OpcionTipo(TipoCliente.Fisica, "Persona física"),
        new OpcionTipo(TipoCliente.Sociedad, "Sociedad")
    };

    // ---- Listado ----

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    /// <summary>Opción elegida en el filtro de tipo ("Todos" por defecto).</summary>
    [ObservableProperty]
    private OpcionTipo _filtroSeleccionado = FiltrosTipo[0];

    public TipoCliente? FiltroTipo => FiltroSeleccionado?.Valor;

    [ObservableProperty]
    private bool _mostrarBajas = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    private ClienteResumen? _seleccionado;

    private int? _idEditando;

    public bool HaySeleccion => Seleccionado is not null;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private string? _aviso;

    public string Recuento => Clientes.Count == 1 ? "1 cliente" : $"{Clientes.Count} clientes";

    partial void OnTextoBusquedaChanged(string value) => _ = BuscarConRetrasoAsync();
    partial void OnFiltroSeleccionadoChanged(OpcionTipo value) => _ = CargarAsync();
    partial void OnMostrarBajasChanged(bool value) => _ = CargarAsync();

    public override Task AlEntrarAsync() => CargarAsync();

    /// <summary>Espera un momento mientras se escribe para no consultar en cada tecla.</summary>
    private async Task BuscarConRetrasoAsync()
    {
        _busqueda?.Cancel();
        var cts = _busqueda = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, cts.Token);
            await CargarAsync();
        }
        catch (TaskCanceledException)
        {
        }
    }

    [RelayCommand]
    private async Task CargarAsync()
    {
        var idSeleccionado = Seleccionado?.Id;
        await Ejecutar(async () =>
        {
            var lista = await _clientes.ListarAsync(TextoBusqueda, FiltroTipo, MostrarBajas);
            Clientes.Clear();
            foreach (var c in lista)
                Clientes.Add(c);
            Seleccionado = Clientes.FirstOrDefault(c => c.Id == idSeleccionado);
            OnPropertyChanged(nameof(Recuento));
        });
    }

    // ---- Panel lateral ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelVisible), nameof(TituloPanel), nameof(EsAlta), nameof(MostrarFormularioDatos))]
    private ModoPanelCliente _modo;

    public bool PanelVisible => Modo != ModoPanelCliente.Ninguno;
    public bool EsAlta => Modo == ModoPanelCliente.Alta;
    public bool MostrarFormularioDatos => Modo is ModoPanelCliente.Alta or ModoPanelCliente.Edicion;

    public string TituloPanel => Modo switch
    {
        ModoPanelCliente.Alta => "Nuevo cliente",
        ModoPanelCliente.Edicion => "Editar cliente",
        ModoPanelCliente.Baja => "Dar de baja",
        ModoPanelCliente.Reactivacion => "Reactivar cliente",
        _ => string.Empty
    };

    [ObservableProperty]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private string _nif = string.Empty;

    [ObservableProperty]
    private TipoCliente _tipo = TipoCliente.Fisica;

    [ObservableProperty]
    private DateTime? _fechaAlta = DateTime.Today;

    [ObservableProperty]
    private string _notas = string.Empty;

    [ObservableProperty]
    private string? _errorPanel;

    /// <summary>Modelos que se pueden marcar en el alta (solo los que aplican al tipo elegido).</summary>
    public ObservableCollection<ModeloSeleccionable> ModelosDisponibles { get; } = new();

    partial void OnTipoChanged(TipoCliente value)
    {
        if (EsAlta) _ = CargarModelosAsync();
    }

    partial void OnFechaAltaChanged(DateTime? oldValue, DateTime? newValue)
    {
        // Los modelos aún sin fecha, o con la misma fecha que el cliente, siguen a la fecha del cliente.
        foreach (var m in ModelosDisponibles.Where(m => m.FechaAlta is null || m.FechaAlta == oldValue))
            m.FechaAlta = newValue;
    }

    [RelayCommand]
    private async Task NuevoAsync()
    {
        Nombre = Nif = Notas = string.Empty;
        Tipo = TipoCliente.Fisica;
        FechaAlta = DateTime.Today;
        ErrorPanel = null;
        Modo = ModoPanelCliente.Alta;
        await CargarModelosAsync();
    }

    private async Task CargarModelosAsync()
    {
        var marcados = ModelosDisponibles.Where(m => m.Marcado).ToDictionary(m => m.Codigo, m => m.FechaAlta);
        var modelos = await _clientes.ModelosParaAsync(Tipo);
        ModelosDisponibles.Clear();
        foreach (var m in modelos)
        {
            var item = new ModeloSeleccionable(m.Codigo, m.Descripcion, m.Periodicidad == Periodicidad.Anual,
                marcados.TryGetValue(m.Codigo, out var f) ? f : FechaAlta);
            item.Marcado = marcados.ContainsKey(m.Codigo);
            ModelosDisponibles.Add(item);
        }
    }

    /// <summary>CU-12: abre el formulario con los datos del cliente seleccionado.</summary>
    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EditarAsync()
    {
        if (Seleccionado is not { } c) return;
        await Ejecutar(async () =>
        {
            var datos = await _clientes.ObtenerDatosAsync(c.Id);
            _idEditando = c.Id;
            Nombre = datos.Nombre;
            Nif = datos.Nif;
            Tipo = datos.Tipo;
            Notas = datos.Notas ?? string.Empty;
            ErrorPanel = null;
            Modo = ModoPanelCliente.Edicion;
        });
    }

    [RelayCommand]
    private void CancelarPanel()
    {
        Modo = ModoPanelCliente.Ninguno;
        ErrorPanel = null;
    }

    [RelayCommand]
    private async Task GuardarPanelAsync()
    {
        ErrorPanel = null;
        Aviso = null;
        Ocupado = true;
        try
        {
            var guardado = await GuardarSegunModoAsync();
            if (guardado is null) return; // cancelado por el usuario

            Modo = ModoPanelCliente.Ninguno;
            Seleccionado = guardado;
            await CargarAsync();
        }
        catch (Exception ex) when (ex is ReglaNegocioException or AccesoDenegadoException)
        {
            ErrorPanel = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorPanel = $"No se ha podido guardar: {ex.GetBaseException().Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>Guarda según el modo del panel. Devuelve null si el usuario cancela.</summary>
    private async Task<ClienteResumen?> GuardarSegunModoAsync()
    {
        switch (Modo)
        {
            case ModoPanelCliente.Alta:
                return await GuardarAltaAsync();
            case ModoPanelCliente.Edicion:
                return await GuardarEdicionAsync();
            default:
                return null;
        }
    }

    /// <summary>CU-11.</summary>
    private async Task<ClienteResumen?> GuardarAltaAsync()
    {
        if (FechaAlta is not DateTime fecha)
            throw new ReglaNegocioException("Indica la fecha de alta.");

        var modelos = new List<ModeloAlta>();
        foreach (var m in ModelosDisponibles.Where(m => m.Marcado))
        {
            if (m.FechaAlta is not DateTime f)
                throw new ReglaNegocioException($"Indica la fecha de alta del modelo {m.Codigo}.");
            modelos.Add(new ModeloAlta(m.Codigo, DateOnly.FromDateTime(f)));
        }

        if (!await ConfirmarNifDuplicadoAsync(excluirId: null))
            return null;

        var creado = await _clientes.CrearAsync(new DatosCliente(Nombre, Nif, Tipo, Notas), DateOnly.FromDateTime(fecha), modelos);
        Aviso = $"Se ha dado de alta a {creado.Nombre}.";
        return creado;
    }

    /// <summary>CU-12.</summary>
    private async Task<ClienteResumen?> GuardarEdicionAsync()
    {
        if (_idEditando is not int id) return null;
        if (!await ConfirmarNifDuplicadoAsync(excluirId: id))
            return null;

        var editado = await _clientes.EditarAsync(id, new DatosCliente(Nombre, Nif, Tipo, Notas));
        Aviso = $"Se han guardado los cambios de {editado.Nombre}.";
        return editado;
    }

    /// <summary>Si otro cliente tiene el mismo NIF, avisa y pregunta si continuar (no es bloqueante).</summary>
    private async Task<bool> ConfirmarNifDuplicadoAsync(int? excluirId)
    {
        if (!ValidadorNif.EsValido(Nif))
            return true; // el servicio dará el error de formato

        var otro = await _clientes.BuscarNifDuplicadoAsync(Nif, excluirId);
        return otro is null || _dialogos.Confirmar("NIF duplicado",
            $"Ya existe el cliente «{otro.Nombre}» con el NIF {otro.Nif}. ¿Quieres guardarlo de todos modos?");
    }

    private async Task Ejecutar(Func<Task> accion)
    {
        Error = null;
        Ocupado = true;
        try
        {
            await accion();
        }
        catch (Exception ex) when (ex is ReglaNegocioException or AccesoDenegadoException)
        {
            Error = ex.Message;
            Aviso = null;
        }
        catch (Exception ex)
        {
            Error = $"Ha ocurrido un error: {ex.GetBaseException().Message}";
            Aviso = null;
        }
        finally
        {
            Ocupado = false;
        }
    }
}
