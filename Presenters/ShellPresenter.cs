namespace Hiker.Presenters;

/// <summary>Que hacer al pulsar atras (constitucion Mobile 7).</summary>
public enum BackAction
{
    /// <summary>Cerrar el menu lateral.</summary>
    CloseFlyout,
    /// <summary>Hay una pregunta cuyo cierre tiraria la ruta: atras no hace nada.</summary>
    Ignore,
    /// <summary>Cerrar el dialogo de encima, como si se tocara fuera.</summary>
    DismissDialog,
    /// <summary>Volver a la pagina anterior de la pila (ficha de ruta → Rutas).</summary>
    PopPage,
    /// <summary>Volver al mapa desde Rutas, Configuracion o Acerca de.</summary>
    GoHome,
    /// <summary>En el mapa: la app se oculta sin cerrarse (nunca para una grabacion).</summary>
    MoveToBack,
}

/// <summary>Textos del menu lateral.</summary>
public sealed record MenuTexts(string Map, string Record, string Stop, string Follow, string Heading,
    string Save, string Clear, string Routes, string Settings, string About);

/// <summary>
/// La logica del menu lateral (antes en AppShell.xaml.cs): textos, los interruptores Seguir y
/// Rumbo, las explicaciones de cada accion y el orden del boton de atras.
/// </summary>
public sealed class ShellPresenter
{
    private static readonly Dictionary<string, (string Title, string Message)> Info = new()
    {
        ["map"] = ("Mapa", "Muestra el mapa a pantalla completa sin iniciar ninguna grabación."),
        ["play"] = ("Grabar", "Comienza a grabar tu recorrido registrando los puntos GPS."),
        ["stop"] = ("Parar", "Detiene la grabación del recorrido en curso."),
        ["save"] = ("Guardar", "Guarda el recorrido grabado como una ruta con nombre."),
        ["clear"] = ("Borrar", "Elimina del mapa el recorrido actual sin guardarlo."),
        ["follow"] = ("Seguir", "Mantiene el mapa centrado automáticamente en tu ubicación en vivo."),
        ["heading"] = ("Rumbo", "Rota el mapa para que la dirección hacia la que miras apunte hacia arriba."),
    };

    private readonly Func<string, string> _translate;

    public ShellPresenter(Func<string, string> translate) => _translate = translate;

    public bool FollowEnabled { get; private set; } = true;   // el mapa arranca siguiendo al usuario
    public bool HeadingEnabled { get; private set; } = true;  // modo Rumbo activado por defecto

    private string L(string phrase) => _translate(phrase);

    public string FollowText => FollowEnabled ? L("Seguir: Sí") : L("Seguir: No");
    public string HeadingText => HeadingEnabled ? L("Rumbo: Sí") : L("Rumbo: No");

    /// <summary>Textos del menu en el idioma activo (al crear el Shell y al cambiar de idioma).</summary>
    public MenuTexts Texts() => new(L("Mapa"), L("Grabar"), L("Parar"), FollowText, HeadingText,
        L("Guardar"), L("Borrar"), L("Rutas"), L("Configuración"), L("Acerca de"));

    public bool ToggleFollow() => FollowEnabled = !FollowEnabled;
    public bool ToggleHeading() => HeadingEnabled = !HeadingEnabled;

    /// <summary>Titulo y explicacion del icono (i) de una accion del submenu del mapa.</summary>
    public (string Title, string Message) InfoFor(string action) =>
        Info.TryGetValue(action, out var info) ? (L(info.Title), L(info.Message)) : (action, string.Empty);

    public string Understood => L("Entendido");

    /// <summary>
    /// Atras, por orden: cierra el menu lateral si esta abierto; cierra el dialogo que haya encima
    /// (salvo las preguntas que al cerrarse tirarian una ruta); en la ficha de una ruta, vuelve a
    /// Rutas; en Rutas, Configuracion o Acerca de, vuelve al mapa; y en el mapa, la app se oculta.
    /// </summary>
    public static BackAction DecideBack(bool flyoutOpen, bool dialogOpen, bool keepOrDiscardOpen,
        int navigationDepth, bool onHome)
    {
        if (flyoutOpen)
            return BackAction.CloseFlyout;
        if (dialogOpen)
            return keepOrDiscardOpen ? BackAction.Ignore : BackAction.DismissDialog;
        if (navigationDepth > 1)
            return BackAction.PopPage;
        return onHome ? BackAction.MoveToBack : BackAction.GoHome;
    }
}
