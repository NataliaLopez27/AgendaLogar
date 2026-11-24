using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AgendaLogar.App.Conversores;

/// <summary>true → Visible, false → Collapsed. Con ConverterParameter="Invertir" hace lo contrario.</summary>
public class BoolAVisibilidad : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var b = value is true;
        if (parameter as string == "Invertir") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Texto vacío o nulo → Collapsed.</summary>
public class TextoAVisibilidad : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Devuelve true si el valor es igual al parámetro (para marcar la opción activa del menú).</summary>
public class IgualA : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
