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
    Reactivacion,
    Modelos
}

/// <summary>Opción de los desplegables de tipo de cliente. Valor nulo = todos.</summary>
public sealed record OpcionTipo(TipoCliente? Valor, string Texto);

/// <summary>Opción del filtro de estado.</summary>
public sealed record OpcionEstado(FiltroEstadoCliente Valor, string Texto);

/// <summary>Opción del filtro de empleado habitual: todos, sin asignar o un empleado concreto.</summary>
public sealed record OpcionEmpleado(int? Id, bool SinAsignar, string Texto)
{
    public static readonly OpcionEmpleado Todos = new(null, false, "Todos los empleados");
    public static readonly OpcionEmpleado NingunoAsignado = new(null, true, ClienteResumen.SinAsignar);
}

/// <summary>Pantalla de clientes del administrador: listado, alta (CU-11), edición (CU-12) y baja/reactivación (CU-13).</summary>
[SoloAdministrador]
public partial class ClientesViewModel : ViewModelBase
{
    private readonly ServicioClientes _clientes;
    private readonly IDialogos _dialogos;
    private readonly ServicioPeriodos? _periodos;
    private CancellationTokenSource? _busqueda;

    public ClientesViewModel(ServicioClientes clientes, IDialogos dialogos, ServicioPeriodos? periodos = null)
    {
        _clientes = clientes;
        _dialogos = dialogos;
        _periodos = periodos;
    }

    public ObservableCollection<ClienteResumen> Clientes { get; } = new();

    public static IReadOnlyList<OpcionTipo> FiltrosTipo { get; } = new[]
    {
        new OpcionTipo(null, "Todos"),
        new OpcionTipo(TipoCliente.Fisica, "Personas físicas"),
        new OpcionTipo(TipoCliente.Sociedad, "Sociedades")
    };

    public static IReadOnlyList<OpcionEstado> FiltrosEstado { get; } = new[]
    {
        new OpcionEstado(FiltroEstadoCliente.Todos, "Todos los estados"),
        new OpcionEstado(FiltroEstadoCliente.Activos, "Activos"),
        new OpcionEstado(FiltroEstadoCliente.DeBaja, "De baja")
    };

    /// <summary>Opciones del filtro de empleado: "Todos", "Sin asignar" y cada empleado.</summary>
    public ObservableCollection<OpcionEmpleado> FiltrosEmpleado { get; } = new() { OpcionEmpleado.Todos, OpcionEmpleado.NingunoAsignado };

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
    private OpcionEstado _filtroEstado = FiltrosEstado[0];

    [ObservableProperty]
    private OpcionEmpleado _filtroEmpleado = OpcionEmpleado.Todos;

    /// <summary>Filtro completo que se envía al servicio (RF-25).</summary>
    public FiltroClientes Filtro => new(
        TextoBusqueda,
        FiltroTipo,
        FiltroEstado?.Valor ?? FiltroEstadoCliente.Todos,
        FiltroEmpleado?.Id,
        FiltroEmpleado?.SinAsignar == true);

    public bool HayFiltros => !string.IsNullOrWhiteSpace(TextoBusqueda) || FiltroTipo is not null
        || FiltroEstado?.Valor is not (null or FiltroEstadoCliente.Todos) || FiltroEmpleado != OpcionEmpleado.Todos;

    [RelayCommand]
    private void LimpiarFiltros()
    {
        _limpiando = true;
        TextoBusqueda = string.Empty;
        FiltroSeleccionado = FiltrosTipo[0];
        FiltroEstado = FiltrosEstado[0];
        FiltroEmpleado = OpcionEmpleado.Todos;
        _limpiando = false;
        _ = CargarAsync();
    }

    private bool _limpiando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand), nameof(DarDeBajaCommand), nameof(ReactivarCommand), nameof(GestionarModelosCommand))]
    private ClienteResumen? _seleccionado;

    /// <summary>Historial de altas y bajas del cliente seleccionado (CU-13).</summary>
    public ObservableCollection<TramoAlta> Historial { get; } = new();

    /// <summary>
    /// Mientras se recarga el listado, la tabla deselecciona y vuelve a seleccionar la fila:
    /// eso no debe cerrar el panel abierto.
    /// </summary>
    private bool _recargando;

    partial void OnSeleccionadoChanged(ClienteResumen? value)
    {
        if (_recargando) return;
        if (Modo is ModoPanelCliente.Baja or ModoPanelCliente.Reactivacion or ModoPanelCliente.Modelos
            && value?.Id != _idEditando)
            Modo = ModoPanelCliente.Ninguno;
    }

    private int? _idEditando;

    public bool HaySeleccion => Seleccionado is not null;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private string? _aviso;

    public string Recuento => Clientes.Count == 1 ? "1 cliente" : $"{Clientes.Count} clientes";

    /// <summary>Mensaje cuando la lista está vacía.</summary>
    public bool ListaVacia => Clientes.Count == 0;

    partial void OnTextoBusquedaChanged(string value)
    {
        if (!_limpiando) _ = BuscarConRetrasoAsync();
    }

    partial void OnFiltroSeleccionadoChanged(OpcionTipo value)
    {
        if (!_limpiando) _ = CargarAsync();
    }

    partial void OnFiltroEstadoChanged(OpcionEstado value)
    {
        if (!_limpiando) _ = CargarAsync();
    }

    partial void OnFiltroEmpleadoChanged(OpcionEmpleado value)
    {
        if (!_limpiando && value is not null) _ = CargarAsync();
    }

    public override async Task AlEntrarAsync()
    {
        await Ejecutar(async () =>
        {
            foreach (var e in await _clientes.EmpleadosParaFiltroAsync())
                FiltrosEmpleado.Add(new OpcionEmpleado(e.Id, false, e.Nombre));
        });
        await CargarAsync();
    }

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
            var lista = await _clientes.ListarAsync(Filtro);
            _recargando = true;
            try
            {
                Clientes.Clear();
                foreach (var c in lista)
                    Clientes.Add(c);
                Seleccionado = Clientes.FirstOrDefault(c => c.Id == idSeleccionado);
            }
            finally
            {
                _recargando = false;
            }
            OnPropertyChanged(nameof(Recuento));
            OnPropertyChanged(nameof(HayFiltros));
            OnPropertyChanged(nameof(ListaVacia));
        });
    }

    // ---- Panel lateral ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelVisible), nameof(TituloPanel), nameof(EsAlta), nameof(MostrarFormularioDatos),
        nameof(MostrarBajaReactivacion), nameof(EtiquetaFecha), nameof(Explicacion), nameof(EsModelos),
        nameof(MostrarGuardar), nameof(TextoCancelar))]
    private ModoPanelCliente _modo;

    public bool MostrarBajaReactivacion => Modo is ModoPanelCliente.Baja or ModoPanelCliente.Reactivacion;

    // ---- Modelos del cliente (CU-14) ----

    public bool EsModelos => Modo == ModoPanelCliente.Modelos;

    /// <summary>En el panel de modelos cada acción se guarda al momento, así que no hay botón "Guardar".</summary>
    public bool MostrarGuardar => Modo != ModoPanelCliente.Modelos;
    public string TextoCancelar => Modo == ModoPanelCliente.Modelos ? "Cerrar" : "Cancelar";

    public ObservableCollection<TramoModelo> ModelosCliente { get; } = new();

    /// <summary>Modelos del catálogo que aplican al tipo del cliente, para añadir.</summary>
    public ObservableCollection<Modelo> ModelosParaAnadir { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DarDeBajaModeloCommand), nameof(GuardarNotasModeloCommand))]
    [NotifyPropertyChangedFor(nameof(HayTramoSeleccionado))]
    private TramoModelo? _tramoSeleccionado;

    public bool HayTramoSeleccionado => TramoSeleccionado is not null;

    partial void OnTramoSeleccionadoChanged(TramoModelo? value)
    {
        NotasModelo = value?.Notas ?? string.Empty;
        FechaBajaModelo = DateTime.Today;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnadirModeloCommand))]
    private Modelo? _modeloAnadir;

    [ObservableProperty]
    private DateTime? _fechaAltaModelo = DateTime.Today;

    [ObservableProperty]
    private DateTime? _fechaBajaModelo = DateTime.Today;

    [ObservableProperty]
    private string _notasModelo = string.Empty;

    // ---- Baja y reactivación (CU-13) ----

    /// <summary>Fecha de baja o de nueva alta, según el modo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Explicacion))]
    private DateTime? _fechaOperacion = DateTime.Today;

    public string NombreSeleccionado => Seleccionado?.Nombre ?? string.Empty;

    public string EtiquetaFecha => Modo == ModoPanelCliente.Baja ? "Fecha de baja" : "Nueva fecha de alta";

    /// <summary>Explica en qué periodos aparecerá el cliente según RN-01 / RN-03.</summary>
    public string Explicacion
    {
        get
        {
            if (FechaOperacion is not DateTime f) return string.Empty;
            var fecha = DateOnly.FromDateTime(f);
            var trimestre = Periodo.TrimestreDe(fecha);
            return Modo == ModoPanelCliente.Baja
                ? $"Seguirá apareciendo en el {trimestre.Nombre} y en el Anual {fecha.Year}, porque estuvo de alta parte de ese periodo. " +
                  "Desde el trimestre siguiente ya no aparecerá. Su histórico se conserva."
                : $"Volverá a aparecer desde el {trimestre.Nombre}. Se conserva el historial de altas y bajas.";
        }
    }

    public bool PanelVisible => Modo != ModoPanelCliente.Ninguno;
    public bool EsAlta => Modo == ModoPanelCliente.Alta;
    public bool MostrarFormularioDatos => Modo is ModoPanelCliente.Alta or ModoPanelCliente.Edicion;

    public string TituloPanel => Modo switch
    {
        ModoPanelCliente.Alta => "Nuevo cliente",
        ModoPanelCliente.Edicion => "Editar cliente",
        ModoPanelCliente.Baja => "Dar de baja",
        ModoPanelCliente.Reactivacion => "Reactivar cliente",
        ModoPanelCliente.Modelos => "Modelos del cliente",
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

    private bool PuedeDarDeBaja() => Seleccionado is { Activo: true };
    private bool PuedeReactivar() => Seleccionado is { Activo: false };

    [RelayCommand(CanExecute = nameof(PuedeDarDeBaja))]
    private Task DarDeBajaAsync() => AbrirBajaReactivacionAsync(ModoPanelCliente.Baja);

    [RelayCommand(CanExecute = nameof(PuedeReactivar))]
    private Task ReactivarAsync() => AbrirBajaReactivacionAsync(ModoPanelCliente.Reactivacion);

    private async Task AbrirBajaReactivacionAsync(ModoPanelCliente modo)
    {
        if (Seleccionado is not { } c) return;
        await Ejecutar(async () =>
        {
            var historial = await _clientes.HistorialAltasAsync(c.Id);
            Historial.Clear();
            foreach (var t in historial)
                Historial.Add(t);
            _idEditando = c.Id;
            FechaOperacion = DateTime.Today;
            ErrorPanel = null;
            Modo = modo;
            OnPropertyChanged(nameof(NombreSeleccionado));
        });
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task GestionarModelosAsync()
    {
        if (Seleccionado is not { } c) return;
        await Ejecutar(async () =>
        {
            _idEditando = c.Id;
            var aplicables = await _clientes.ModelosParaAsync(c.Tipo);
            ModelosParaAnadir.Clear();
            foreach (var m in aplicables)
                ModelosParaAnadir.Add(m);
            ModeloAnadir = null;
            FechaAltaModelo = DateTime.Today;
            await RecargarModelosClienteAsync();
            ErrorPanel = null;
            Modo = ModoPanelCliente.Modelos;
            OnPropertyChanged(nameof(NombreSeleccionado));
        });
    }

    private async Task RecargarModelosClienteAsync(int? seleccionarId = null)
    {
        if (_idEditando is not int id) return;
        var tramos = await _clientes.ModelosDeClienteAsync(id);
        ModelosCliente.Clear();
        foreach (var t in tramos)
            ModelosCliente.Add(t);
        TramoSeleccionado = ModelosCliente.FirstOrDefault(t => t.Id == seleccionarId);
    }

    private bool PuedeAnadirModelo() => ModeloAnadir is not null;

    [RelayCommand(CanExecute = nameof(PuedeAnadirModelo))]
    private Task AnadirModeloAsync() => EjecutarEnPanel(async () =>
    {
        if (_idEditando is not int id || ModeloAnadir is not { } m) return;
        if (FechaAltaModelo is not DateTime f)
            throw new ReglaNegocioException("Indica la fecha de alta del modelo.");
        var tramo = await _clientes.AnadirModeloAsync(id, m.Codigo, DateOnly.FromDateTime(f));
        Aviso = $"Modelo {m.Codigo} añadido desde el {tramo.FechaAlta:dd/MM/yyyy}.";
        ModeloAnadir = null;
        await RecargarModelosClienteAsync(tramo.Id);
    });

    private bool PuedeDarDeBajaModelo() => TramoSeleccionado is { Vigente: true };

    [RelayCommand(CanExecute = nameof(PuedeDarDeBajaModelo))]
    private Task DarDeBajaModeloAsync() => EjecutarEnPanel(async () =>
    {
        if (TramoSeleccionado is not { } t) return;
        if (FechaBajaModelo is not DateTime f)
            throw new ReglaNegocioException("Indica la fecha de baja del modelo.");
        await _clientes.DarDeBajaModeloAsync(t.Id, DateOnly.FromDateTime(f));
        Aviso = $"Modelo {t.Codigo} de baja desde el {f:dd/MM/yyyy}.";
        await RecargarModelosClienteAsync(t.Id);
    });

    [RelayCommand(CanExecute = nameof(HayTramoSeleccionado))]
    private Task GuardarNotasModeloAsync() => EjecutarEnPanel(async () =>
    {
        if (TramoSeleccionado is not { } t) return;
        await _clientes.CambiarNotasModeloAsync(t.Id, NotasModelo);
        Aviso = $"Notas del modelo {t.Codigo} guardadas.";
        await RecargarModelosClienteAsync(t.Id);
    });

    /// <summary>Ejecuta una acción del panel de modelos y refresca el listado (columna "Modelos").</summary>
    private async Task EjecutarEnPanel(Func<Task> accion)
    {
        ErrorPanel = null;
        Ocupado = true;
        try
        {
            await accion();
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
            case ModoPanelCliente.Baja:
            case ModoPanelCliente.Reactivacion:
                return await GuardarBajaReactivacionAsync();
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
        await ProponerPeriodosAbiertosAsync(creado);
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

    /// <summary>CU-13.</summary>
    private async Task<ClienteResumen?> GuardarBajaReactivacionAsync()
    {
        if (_idEditando is not int id) return null;
        if (FechaOperacion is not DateTime f)
            throw new ReglaNegocioException($"Indica la {EtiquetaFecha.ToLower()}.");
        var fecha = DateOnly.FromDateTime(f);

        if (Modo == ModoPanelCliente.Baja)
        {
            var c = await _clientes.DarDeBajaAsync(id, fecha);
            Aviso = $"{c.Nombre} queda de baja desde el {fecha:dd/MM/yyyy}.";
            return c;
        }

        var r = await _clientes.ReactivarAsync(id, fecha);
        Aviso = $"{r.Nombre} vuelve a estar de alta desde el {fecha:dd/MM/yyyy}.";
        await ProponerPeriodosAbiertosAsync(r);
        return r;
    }

    /// <summary>
    /// RN-04: si el cliente es vigente en periodos ya creados y abiertos en los que todavía no está,
    /// propone añadirlo (pendiente y sin asignar; se asigna después en Periodos).
    /// </summary>
    private async Task ProponerPeriodosAbiertosAsync(ClienteResumen cliente)
    {
        if (_periodos is null) return;
        var periodos = await _periodos.PeriodosAbiertosSinClienteAsync(cliente.Id);
        if (periodos.Count == 0) return;

        var nombres = string.Join(", ", periodos.Select(p => p.Nombre));
        if (!_dialogos.Confirmar("Añadir a periodos abiertos",
                $"{cliente.Nombre} está de alta en periodos que ya están creados y abiertos ({nombres}). " +
                "¿Quieres añadirlo a esos periodos? Quedará pendiente y sin asignar; podrás asignarlo en Periodos."))
            return;

        await _periodos.AnadirClienteAPeriodosAsync(cliente.Id, periodos.Select(p => p.Id).ToList());
        Aviso += $" Añadido a: {nombres}.";
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
