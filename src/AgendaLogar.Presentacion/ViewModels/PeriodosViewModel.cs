using System.Collections.ObjectModel;
using System.ComponentModel;
using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.Navegacion;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>Fila de la tabla de asignaciones con su casilla de selección (asignación masiva).</summary>
public partial class FilaAsignacion : ObservableObject
{
    public FilaAsignacion(AsignacionFila fila) => Fila = fila;

    public AsignacionFila Fila { get; }

    [ObservableProperty]
    private bool _seleccionada;
}

/// <summary>
/// Pantalla de periodos del administrador: crear periodo (CU-15, RF-41), asignar clientes (CU-16)
/// y cerrar o reabrir (CU-17).
/// </summary>
[SoloAdministrador]
public partial class PeriodosViewModel : ViewModelBase
{
    private readonly ServicioPeriodos _periodos;
    private readonly IDialogos _dialogos;
    private CancellationTokenSource? _busqueda;

    public PeriodosViewModel(ServicioPeriodos periodos, IDialogos dialogos)
    {
        _periodos = periodos;
        _dialogos = dialogos;
    }

    public ObservableCollection<PeriodoResumen> Periodos { get; } = new();
    public ObservableCollection<FilaAsignacion> Filas { get; } = new();
    public ObservableCollection<OpcionEmpleado> FiltrosEmpleado { get; } = new() { OpcionEmpleado.Todos, OpcionEmpleado.NingunoAsignado };
    public ObservableCollection<EmpleadoFiltro> Empleados { get; } = new();

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private string? _aviso;

    public bool HayPeriodos => Periodos.Count > 0;

    // ---------------- Lista de periodos ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion), nameof(PeriodoAbierto), nameof(PeriodoCerrado))]
    [NotifyCanExecuteChangedFor(nameof(CerrarPeriodoCommand), nameof(ReabrirPeriodoCommand),
        nameof(AsignarSeleccionadosCommand), nameof(QuitarAsignacionCommand))]
    private PeriodoResumen? _periodoSeleccionado;

    public bool HaySeleccion => PeriodoSeleccionado is not null;
    public bool PeriodoAbierto => PeriodoSeleccionado?.Abierto == true;
    public bool PeriodoCerrado => PeriodoSeleccionado is { Abierto: false };

    private bool _recargando;

    partial void OnPeriodoSeleccionadoChanged(PeriodoResumen? value)
    {
        if (!_recargando) _ = CargarFilasAsync();
    }

    public override async Task AlEntrarAsync()
    {
        await Ejecutar(async () =>
        {
            foreach (var e in await _periodos.EmpleadosAsignablesAsync())
            {
                Empleados.Add(e);
                FiltrosEmpleado.Add(new OpcionEmpleado(e.Id, false, e.Nombre));
            }
        });
        await CargarPeriodosAsync(seleccionarId: null);
    }

    private async Task CargarPeriodosAsync(int? seleccionarId)
    {
        var id = seleccionarId ?? PeriodoSeleccionado?.Id;
        await Ejecutar(async () =>
        {
            var lista = await _periodos.ListarAsync();
            _recargando = true;
            try
            {
                Periodos.Clear();
                foreach (var p in lista) Periodos.Add(p);
                PeriodoSeleccionado = Periodos.FirstOrDefault(p => p.Id == id) ?? Periodos.FirstOrDefault();
            }
            finally
            {
                _recargando = false;
            }
            OnPropertyChanged(nameof(HayPeriodos));
        });
        await CargarFilasAsync();
    }

    // ---------------- Nuevo periodo (CU-15 y RF-41) ----------------

    [ObservableProperty]
    private bool _mostrarNuevo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NombreNuevo), nameof(EsTrimestral))]
    private bool _nuevoEsAnual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NombreNuevo))]
    private int _nuevoAnio = DateTime.Today.Year;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NombreNuevo))]
    private int _nuevoTrimestre = (DateTime.Today.Month - 1) / 3 + 1;

    [ObservableProperty]
    private bool _copiarAnterior = true;

    public bool EsTrimestral => !NuevoEsAnual;
    public IReadOnlyList<int> Trimestres { get; } = new[] { 1, 2, 3, 4 };
    public IReadOnlyList<int> Anios { get; } = Enumerable.Range(DateTime.Today.Year - 2, 5).ToList();

    public string NombreNuevo => NuevoEsAnual ? $"Anual {NuevoAnio}" : $"{NuevoTrimestre}T {NuevoAnio}";

    [RelayCommand]
    private void AbrirNuevo()
    {
        // Propone el trimestre en curso; si ya existe, el siguiente que falte.
        var hoy = Periodo.TrimestreDe(DateOnly.FromDateTime(DateTime.Today));
        var (anio, t) = (hoy.Anio, (int)hoy.Trimestre!.Value);
        while (Periodos.Any(p => p.Anio == anio && p.Trimestre == t))
            (anio, t) = t == 4 ? (anio + 1, 1) : (anio, t + 1);
        NuevoEsAnual = false;
        NuevoAnio = anio;
        NuevoTrimestre = t;
        CopiarAnterior = true;
        Error = null;
        MostrarNuevo = true;
    }

    [RelayCommand]
    private void CancelarNuevo() => MostrarNuevo = false;

    [RelayCommand]
    private async Task CrearPeriodoAsync()
    {
        ResultadoCreacionPeriodo? r = null;
        await Ejecutar(async () =>
        {
            r = await _periodos.CrearAsync(NuevoAnio, NuevoEsAnual ? null : (byte)NuevoTrimestre, CopiarAnterior);
            Aviso = r.Resumen;
            MostrarNuevo = false;
        });
        if (r is not null)
            await CargarPeriodosAsync(r.PeriodoId);
    }

    // ---------------- Tabla de asignaciones y filtros ----------------

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private OpcionTipo _filtroTipo = ClientesViewModel.FiltrosTipo[0];

    [ObservableProperty]
    private OpcionEmpleado _filtroEmpleado = OpcionEmpleado.Todos;

    partial void OnTextoBusquedaChanged(string value) => _ = BuscarConRetrasoAsync();
    partial void OnFiltroTipoChanged(OpcionTipo value) => _ = CargarFilasAsync();
    partial void OnFiltroEmpleadoChanged(OpcionEmpleado value)
    {
        if (value is not null) _ = CargarFilasAsync();
    }

    private async Task BuscarConRetrasoAsync()
    {
        _busqueda?.Cancel();
        var cts = _busqueda = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, cts.Token);
            await CargarFilasAsync();
        }
        catch (TaskCanceledException)
        {
        }
    }

    public string RecuentoFilas => Filas.Count == 1 ? "1 cliente" : $"{Filas.Count} clientes";
    public int Seleccionadas => Filas.Count(f => f.Seleccionada);
    public string TextoSeleccion => Seleccionadas == 1 ? "1 seleccionado" : $"{Seleccionadas} seleccionados";

    [ObservableProperty]
    private bool _todosSeleccionados;

    partial void OnTodosSeleccionadosChanged(bool value)
    {
        foreach (var f in Filas) f.Seleccionada = value;
    }

    [RelayCommand]
    public async Task CargarFilasAsync()
    {
        if (PeriodoSeleccionado is not { } p)
        {
            Filas.Clear();
            NotificarFilas();
            return;
        }

        await Ejecutar(async () =>
        {
            var filtro = new FiltroAsignaciones(TextoBusqueda, FiltroTipo?.Valor, FiltroEmpleado?.Id, FiltroEmpleado?.SinAsignar == true);
            var filas = await _periodos.AsignacionesAsync(p.Id, filtro);
            foreach (var f in Filas) f.PropertyChanged -= AlCambiarFila;
            Filas.Clear();
            foreach (var f in filas)
            {
                var item = new FilaAsignacion(f);
                item.PropertyChanged += AlCambiarFila;
                Filas.Add(item);
            }
        });
        _todosSeleccionados = false;
        OnPropertyChanged(nameof(TodosSeleccionados));
        NotificarFilas();
    }

    private void AlCambiarFila(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilaAsignacion.Seleccionada)) NotificarSeleccion();
    }

    private void NotificarFilas()
    {
        OnPropertyChanged(nameof(RecuentoFilas));
        NotificarSeleccion();
    }

    private void NotificarSeleccion()
    {
        OnPropertyChanged(nameof(Seleccionadas));
        OnPropertyChanged(nameof(TextoSeleccion));
        AsignarSeleccionadosCommand.NotifyCanExecuteChanged();
        QuitarAsignacionCommand.NotifyCanExecuteChanged();
    }

    // ---------------- Asignar (CU-16) ----------------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AsignarSeleccionadosCommand))]
    private EmpleadoFiltro? _empleadoDestino;

    private bool PuedeAsignar() => PeriodoAbierto && Seleccionadas > 0 && EmpleadoDestino is not null;
    private bool PuedeQuitar() => PeriodoAbierto && Seleccionadas > 0;

    [RelayCommand(CanExecute = nameof(PuedeAsignar))]
    private Task AsignarSeleccionadosAsync() => AsignarAsync(EmpleadoDestino!.Id, EmpleadoDestino.Nombre);

    [RelayCommand(CanExecute = nameof(PuedeQuitar))]
    private Task QuitarAsignacionAsync() => AsignarAsync(null, null);

    private async Task AsignarAsync(int? usuarioId, string? nombre)
    {
        if (PeriodoSeleccionado is not { } p) return;
        var ids = Filas.Where(f => f.Seleccionada).Select(f => f.Fila.Id).ToList();
        var hecho = false;
        await Ejecutar(async () =>
        {
            var n = await _periodos.AsignarAsync(p.Id, ids, usuarioId);
            Aviso = usuarioId is null
                ? $"{n} cliente(s) quedan sin asignar."
                : $"{n} cliente(s) asignados a {nombre}.";
            hecho = true;
        });
        if (hecho) await CargarPeriodosAsync(p.Id);
    }

    // ---------------- Cerrar y reabrir (CU-17) ----------------

    [RelayCommand(CanExecute = nameof(PeriodoAbierto))]
    private async Task CerrarPeriodoAsync()
    {
        if (PeriodoSeleccionado is not { } p) return;
        var mensaje = p.Pendientes > 0
            ? $"Quedan {p.Pendientes} cliente(s) pendientes en el {p.Nombre}. Si lo cierras, los empleados ya no podrán cambiarlos. ¿Cerrar de todos modos?"
            : $"¿Cerrar el {p.Nombre}? Quedará en solo lectura para los empleados.";
        if (!_dialogos.Confirmar("Cerrar periodo", mensaje)) return;

        var hecho = false;
        await Ejecutar(async () =>
        {
            await _periodos.CerrarAsync(p.Id);
            Aviso = $"El {p.Nombre} está cerrado.";
            hecho = true;
        });
        if (hecho) await CargarPeriodosAsync(p.Id);
    }

    [RelayCommand(CanExecute = nameof(PeriodoCerrado))]
    private async Task ReabrirPeriodoAsync()
    {
        if (PeriodoSeleccionado is not { } p) return;
        var hecho = false;
        await Ejecutar(async () =>
        {
            await _periodos.ReabrirAsync(p.Id);
            Aviso = $"El {p.Nombre} vuelve a estar abierto.";
            hecho = true;
        });
        if (hecho) await CargarPeriodosAsync(p.Id);
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
