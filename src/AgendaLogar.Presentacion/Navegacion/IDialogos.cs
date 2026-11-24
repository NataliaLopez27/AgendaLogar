namespace AgendaLogar.Presentacion.Navegacion;

/// <summary>Diálogos de confirmación. En la aplicación se muestran ventanas; en los tests se simulan.</summary>
public interface IDialogos
{
    bool Confirmar(string titulo, string mensaje);
}
