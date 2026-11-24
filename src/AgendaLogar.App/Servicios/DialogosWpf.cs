using System.Windows;
using AgendaLogar.Presentacion.Navegacion;

namespace AgendaLogar.App.Servicios;

/// <summary>Diálogos de confirmación con ventanas de Windows.</summary>
public class DialogosWpf : IDialogos
{
    public bool Confirmar(string titulo, string mensaje) =>
        MessageBox.Show(Application.Current.MainWindow!, mensaje, titulo,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
}
