using System.Windows;
using AgendaLogar.App.Controles;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Icono de la ventana y de la barra de tareas: Recursos/icono.png o el logo.
        if (Marca.Icono is { } icono)
            Icon = icono;
    }
}
