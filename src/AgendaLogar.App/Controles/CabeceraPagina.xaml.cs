using System.Windows;
using System.Windows.Controls;

namespace AgendaLogar.App.Controles;

/// <summary>Cabecera de página: icono, título, subtítulo y, opcionalmente, botones a la derecha.</summary>
public partial class CabeceraPagina : UserControl
{
    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(CabeceraPagina));
    public static readonly DependencyProperty SubtituloProperty =
        DependencyProperty.Register(nameof(Subtitulo), typeof(string), typeof(CabeceraPagina));
    public static readonly DependencyProperty IconoProperty =
        DependencyProperty.Register(nameof(Icono), typeof(string), typeof(CabeceraPagina));
    public static readonly DependencyProperty AccionesProperty =
        DependencyProperty.Register(nameof(Acciones), typeof(object), typeof(CabeceraPagina));

    public CabeceraPagina() => InitializeComponent();

    public string? Titulo { get => (string?)GetValue(TituloProperty); set => SetValue(TituloProperty, value); }
    public string? Subtitulo { get => (string?)GetValue(SubtituloProperty); set => SetValue(SubtituloProperty, value); }
    public string? Icono { get => (string?)GetValue(IconoProperty); set => SetValue(IconoProperty, value); }
    public object? Acciones { get => GetValue(AccionesProperty); set => SetValue(AccionesProperty, value); }
}
