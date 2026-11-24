using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AgendaLogar.App.Controles;

/// <summary>Carga las imágenes de la marca desde la carpeta Recursos, si existen.</summary>
public static class Marca
{
    private static readonly Lazy<ImageSource?> _logo = new(() => Cargar("Recursos/logo.png"));
    private static readonly Lazy<ImageSource?> _icono = new(() => Cargar("Recursos/icono.png") ?? Cargar("Recursos/logo.png"));

    /// <summary>Logo original (Recursos/logo.png) o null si no se ha añadido.</summary>
    public static ImageSource? Logo => _logo.Value;

    /// <summary>Icono de la ventana (Recursos/icono.png, o el logo) o null.</summary>
    public static ImageSource? Icono => _icono.Value;

    private static ImageSource? Cargar(string ruta)
    {
        try
        {
            var info = Application.GetResourceStream(new Uri(ruta, UriKind.Relative));
            if (info is null) return null;
            using var stream = info.Stream;
            var imagen = new BitmapImage();
            imagen.BeginInit();
            imagen.CacheOption = BitmapCacheOption.OnLoad;
            imagen.StreamSource = stream;
            imagen.EndInit();
            imagen.Freeze();
            return imagen;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
