using System.Windows;
using AgendaLogar.Presentacion.Navegacion;
using AgendaLogar.Presentacion.ViewModels;
using AgendaLogar.App.Views;
using AgendaLogar.Core.Datos;
using AgendaLogar.Core.Seguridad;
using AgendaLogar.Core.Servicios;
using AgendaLogar.App.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgendaLogar.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((contexto, servicios) => RegistrarServicios(contexto.Configuration, servicios))
            .Build();
    }

    private static void RegistrarServicios(IConfiguration configuracion, IServiceCollection servicios)
    {
        var cadena = configuracion.GetConnectionString("AgendaLogar")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'AgendaLogar' en appsettings.json.");

        // En WPF las ventanas viven mucho tiempo: se usa una fábrica y un DbContext corto por operación.
        servicios.AddDbContextFactory<AgendaLogarDbContext>(opciones =>
            opciones.UseSqlServer(cadena, sql => sql.EnableRetryOnFailure()));
        servicios.AddSingleton(configuracion.GetSection("BaseDatos").Get<OpcionesBaseDatos>() ?? new OpcionesBaseDatos());
        servicios.AddSingleton<InicializadorBaseDatos>();

        // Seguridad y servicios de negocio
        servicios.AddSingleton<IHashPassword, HashPasswordBCrypt>();
        servicios.AddSingleton<ISesion, Sesion>();
        servicios.AddSingleton<ControlAcceso>();
        servicios.AddSingleton<ServicioAutenticacion>();
        servicios.AddSingleton<ServicioUsuarios>();

        // Navegación
        servicios.AddSingleton<INavegacionService, NavegacionService>();
        servicios.AddSingleton<IDialogos, DialogosWpf>();
        servicios.AddSingleton<Func<Type, ViewModelBase>>(sp => tipo => (ViewModelBase)sp.GetRequiredService(tipo));

        // ViewModels y ventana principal
        servicios.AddSingleton<MainViewModel>();
        servicios.AddTransient<ConexionViewModel>();
        servicios.AddTransient<LoginViewModel>();
        servicios.AddTransient<InicioViewModel>();
        servicios.AddTransient<CambiarPasswordViewModel>();
        servicios.AddTransient<EmpleadosViewModel>();
        servicios.AddTransient<CatalogoModelosViewModel>();
        servicios.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var ventana = _host.Services.GetRequiredService<MainWindow>();
        ventana.Show();

        _host.Services.GetRequiredService<INavegacionService>().NavegarA<ConexionViewModel>();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
