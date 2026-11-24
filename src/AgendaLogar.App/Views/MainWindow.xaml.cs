using System.Windows;
using AgendaLogar.Presentacion.ViewModels;

namespace AgendaLogar.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
