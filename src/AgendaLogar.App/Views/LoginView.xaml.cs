using System.Windows.Controls;

namespace AgendaLogar.App.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        Loaded += (_, _) => CajaUsuario.Focus();
    }
}
