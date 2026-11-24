using System.Windows;
using System.Windows.Controls;

namespace AgendaLogar.App.Comportamientos;

/// <summary>
/// Permite enlazar (Binding) la contraseña de un PasswordBox al ViewModel,
/// ya que la propiedad Password no admite enlaces por seguridad.
/// Uso: &lt;PasswordBox ayuda:PasswordBoxAyuda.Password="{Binding Password, Mode=TwoWay}" /&gt;
/// </summary>
public static class PasswordBoxAyuda
{
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached(
        "Password", typeof(string), typeof(PasswordBoxAyuda),
        // El valor por defecto es null (no string.Empty) para que, al enlazarlo con "" desde el ViewModel,
        // se detecte un cambio y se ejecute AlCambiarPassword, que es donde se escucha lo que escribe el usuario.
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, AlCambiarPassword));

    private static readonly DependencyProperty ActualizandoProperty = DependencyProperty.RegisterAttached(
        "Actualizando", typeof(bool), typeof(PasswordBoxAyuda));

    private static readonly DependencyProperty SuscritoProperty = DependencyProperty.RegisterAttached(
        "Suscrito", typeof(bool), typeof(PasswordBoxAyuda));

    public static string? GetPassword(DependencyObject d) => (string?)d.GetValue(PasswordProperty);
    public static void SetPassword(DependencyObject d, string value) => d.SetValue(PasswordProperty, value);

    private static void AlCambiarPassword(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox caja) return;

        if (!(bool)caja.GetValue(SuscritoProperty))
        {
            caja.PasswordChanged += AlEscribir;
            caja.SetValue(SuscritoProperty, true);
        }

        // Si el cambio viene del ViewModel (por ejemplo, para vaciar el campo), se copia a la caja.
        if (!(bool)caja.GetValue(ActualizandoProperty))
            caja.Password = (string?)e.NewValue ?? string.Empty;
    }

    private static void AlEscribir(object sender, RoutedEventArgs e)
    {
        var caja = (PasswordBox)sender;
        caja.SetValue(ActualizandoProperty, true);
        SetPassword(caja, caja.Password);
        caja.SetValue(ActualizandoProperty, false);
    }
}
