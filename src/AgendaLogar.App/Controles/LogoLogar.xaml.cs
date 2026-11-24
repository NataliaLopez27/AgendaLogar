using System.Windows;
using System.Windows.Controls;

namespace AgendaLogar.App.Controles;

public partial class LogoLogar : UserControl
{
    public LogoLogar()
    {
        InitializeComponent();
        if (Marca.Logo is { } logo)
        {
            ImagenLogo.Source = logo;
            ImagenLogo.Visibility = Visibility.Visible;
            LogoVectorial.Visibility = Visibility.Collapsed;
        }
    }
}
