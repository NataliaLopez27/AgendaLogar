using System.Collections.ObjectModel;
using AgendaLogar.Core.Dominio;
using AgendaLogar.Core.Seguridad;
using AgendaLogar.Core.Servicios;
using AgendaLogar.Presentacion.Navegacion;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaLogar.Presentacion.ViewModels;

/// <summary>CU-10 Gestionar empleados: listado, alta, edición, desactivar/reactivar y restablecer contraseña.</summary>
[SoloAdministrador]
public partial class EmpleadosViewModel : ViewModelBase
{
    private readonly ServicioUsuarios _usuarios;
    private readonly IDialogos _dialogos;

    public EmpleadosViewModel(ServicioUsuarios usuarios, IDialogos dialogos)
    {
        _usuarios = usuarios;
        _dialogos = dialogos;
    }

    public ObservableCollection<UsuarioResumen> Usuarios { get; } = new();

    public IReadOnlyList<OpcionRol> Roles { get; } = new[]
    {
        new OpcionRol(Rol.Empleado, "Empleado"),
        new OpcionRol(Rol.Admin, "Administrador")
    };

    [ObservableProperty]
    private bool _mostrarDesactivados;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand), nameof(DesactivarCommand), nameof(ReactivarCommand), nameof(RestablecerPasswordCommand))]
    private UsuarioResumen? _seleccionado;

    public bool HaySeleccion => Seleccionado is not null;

    [ObservableProperty]
    private string? _error;

    /// <summary>Mensaje de confirmación, por ejemplo con la contraseña temporal generada.</summary>
    [ObservableProperty]
    private string? _aviso;

    // ---- Formulario de alta / edición ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloEditor), nameof(EsAlta))]
    private bool _editorVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloEditor), nameof(EsAlta))]
    private int? _idEditando;

    [ObservableProperty]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private string _nombreUsuario = string.Empty;

    [ObservableProperty]
    private Rol _rol = Rol.Empleado;

    [ObservableProperty]
    private string _passwordInicial = string.Empty;

    [ObservableProperty]
    private string? _errorEditor;

    public bool EsAlta => IdEditando is null;
    public string TituloEditor => EsAlta ? "Nuevo empleado" : "Editar empleado";
    public string RequisitosPassword => ReglasPassword.Descripcion;

    public override Task AlEntrarAsync() => CargarAsync();

    partial void OnMostrarDesactivadosChanged(bool value) => _ = CargarAsync();

    [RelayCommand]
    private async Task CargarAsync()
    {
        var idSeleccionado = Seleccionado?.Id;
        await Ejecutar(async () =>
        {
            var lista = await _usuarios.ListarAsync(MostrarDesactivados);
            Usuarios.Clear();
            foreach (var u in lista)
                Usuarios.Add(u);
            Seleccionado = Usuarios.FirstOrDefault(u => u.Id == idSeleccionado);
        });
    }

    [RelayCommand]
    private void Nuevo()
    {
        IdEditando = null;
        Nombre = NombreUsuario = string.Empty;
        Rol = Rol.Empleado;
        PasswordInicial = GeneradorPassword.Generar();
        ErrorEditor = null;
        EditorVisible = true;
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void Editar()
    {
        if (Seleccionado is not { } u) return;
        IdEditando = u.Id;
        Nombre = u.Nombre;
        NombreUsuario = u.NombreUsuario;
        Rol = u.Rol;
        PasswordInicial = string.Empty;
        ErrorEditor = null;
        EditorVisible = true;
    }

    [RelayCommand]
    private void GenerarPassword() => PasswordInicial = GeneradorPassword.Generar();

    [RelayCommand]
    private void CancelarEditor()
    {
        EditorVisible = false;
        ErrorEditor = null;
    }

    [RelayCommand]
    private async Task GuardarEditorAsync()
    {
        ErrorEditor = null;
        Aviso = null;
        Ocupado = true;
        try
        {
            var datos = new DatosUsuario(Nombre, NombreUsuario, Rol);
            UsuarioResumen guardado;
            if (IdEditando is int id)
            {
                guardado = await _usuarios.EditarAsync(id, datos);
                Aviso = $"Se han guardado los cambios de {guardado.Nombre}.";
            }
            else
            {
                guardado = await _usuarios.CrearAsync(datos, PasswordInicial);
                Aviso = $"Se ha creado el usuario «{guardado.NombreUsuario}» con la contraseña temporal {PasswordInicial}. " +
                        "Tendrá que cambiarla la primera vez que entre.";
            }

            EditorVisible = false;
            Seleccionado = guardado;
            await CargarAsync();
        }
        catch (Exception ex) when (ex is ReglaNegocioException or AccesoDenegadoException)
        {
            ErrorEditor = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorEditor = $"No se ha podido guardar: {ex.GetBaseException().Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    private bool PuedeDesactivar() => Seleccionado is { Activo: true };
    private bool PuedeReactivar() => Seleccionado is { Activo: false };

    [RelayCommand(CanExecute = nameof(PuedeDesactivar))]
    private async Task DesactivarAsync()
    {
        if (Seleccionado is not { } u) return;
        if (!_dialogos.Confirmar("Desactivar empleado",
                $"¿Desactivar a {u.Nombre}? No podrá iniciar sesión, pero se conserva su histórico."))
            return;

        await Ejecutar(async () =>
        {
            await _usuarios.DesactivarAsync(u.Id);
            Aviso = $"{u.Nombre} ha sido desactivado.";
            await CargarAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(PuedeReactivar))]
    private async Task ReactivarAsync()
    {
        if (Seleccionado is not { } u) return;
        await Ejecutar(async () =>
        {
            await _usuarios.ReactivarAsync(u.Id);
            Aviso = $"{u.Nombre} vuelve a estar activo.";
            await CargarAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task RestablecerPasswordAsync()
    {
        if (Seleccionado is not { } u) return;
        if (!_dialogos.Confirmar("Restablecer contraseña",
                $"Se generará una contraseña temporal para {u.Nombre} y tendrá que cambiarla al entrar. ¿Continuar?"))
            return;

        var temporal = GeneradorPassword.Generar();
        await Ejecutar(async () =>
        {
            await _usuarios.RestablecerPasswordAsync(u.Id, temporal);
            Aviso = $"Nueva contraseña temporal de {u.Nombre}: {temporal}";
            await CargarAsync();
        });
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

/// <summary>Elemento del desplegable de rol.</summary>
public sealed record OpcionRol(Rol Valor, string Texto);
