using Hiker.Pages;
using Hiker.Services;

namespace Hiker;

public partial class AppShell : Shell
{
    private bool _followEnabled = true;   // el mapa arranca siguiendo al usuario
    private bool _headingEnabled = true;  // modo Rumbo activado por defecto

    public AppShell()
    {
        InitializeComponent();

        // Pie del menu: version dinamica de la app.
        VersionLabel.Text = $"v{Microsoft.Maui.ApplicationModel.AppInfo.Current.VersionString}";

        ApplyTranslations();
    }

    /// <summary>Traduce una frase (la clave es la propia frase en castellano, seccion 8).</summary>
    private static string L(string phrase) =>
        IPlatformApplication.Current?.Services.GetService<TranslationService>()?.Translate(phrase) ?? phrase;

    /// <summary>
    /// Pone los textos del menu en el idioma activo. Se llama al crear el Shell y cada vez que se
    /// cambia el idioma (Configuracion o Acerca de), para que el menu no se quede en el anterior.
    /// </summary>
    public void ApplyTranslations()
    {
        MapLabel.Text = L("Mapa");
        RecordLabel.Text = L("Grabar");
        StopRecordLabel.Text = L("Parar");
        FollowLabel.Text = FollowText();
        HeadingLabel.Text = HeadingText();
        SaveLabel.Text = L("Guardar");
        ClearLabel.Text = L("Borrar");
        RoutesLabel.Text = L("Rutas");
        SettingsLabel.Text = L("Configuración");
        AboutLabel.Text = L("Acerca de");
    }

    private string FollowText() => _followEnabled ? L("Seguir: Sí") : L("Seguir: No");
    private string HeadingText() => _headingEnabled ? L("Rumbo: Sí") : L("Rumbo: No");

    // GPS: despliega/colapsa el submenu de acciones del mapa (no cierra el menu).
    private void OnGpsTapped(object sender, TappedEventArgs e)
    {
        GpsSubmenu.IsVisible = !GpsSubmenu.IsVisible;
        GpsChevron.Text = GpsSubmenu.IsVisible ? "▾" : "▸";
    }

    // Muestra solo el mapa (pantalla del GPS Tracker) sin ejecutar ninguna accion.
    private async void OnActionMap(object sender, TappedEventArgs e) => await NavigateAsync("//HomePage");

    private async void OnActionPlay(object sender, TappedEventArgs e) => await RunMapActionAsync("play");
    private async void OnActionStop(object sender, TappedEventArgs e) => await RunMapActionAsync("stop");
    private async void OnActionSave(object sender, TappedEventArgs e) => await RunMapActionAsync("save");
    private async void OnActionClear(object sender, TappedEventArgs e) => await RunMapActionAsync("clear");

    private async Task RunMapActionAsync(string action)
    {
        FlyoutIsPresented = false;
        if (CurrentPage is not HomePage)
        {
            await GoToAsync("//HomePage");
            // Tras navegar, CurrentPage puede tardar un instante en ser la HomePage ya montada:
            // se espera brevemente en vez de lanzar la accion al vacio (antes se perdia el
            // "Grabar" cuando se pulsaba desde Rutas o Configuracion).
            for (int i = 0; i < 20 && CurrentPage is not HomePage; i++)
                await Task.Delay(50);
        }
        (CurrentPage as HomePage)?.RunMapAction(action);
    }

    // Seguir: alterna el recentrado automatico del mapa en la ubicacion en vivo.
    private async void OnActionFollow(object sender, TappedEventArgs e)
    {
        _followEnabled = !_followEnabled;
        FollowLabel.Text = FollowText();
        FlyoutIsPresented = false;
        if (CurrentPage is not HomePage)
            await GoToAsync("//HomePage");
        (CurrentPage as HomePage)?.SetFollow(_followEnabled);
    }

    // Rumbo: alterna el modo heading-up (rota el mapa segun la brujula).
    private async void OnActionHeading(object sender, TappedEventArgs e)
    {
        _headingEnabled = !_headingEnabled;
        HeadingLabel.Text = HeadingText();
        FlyoutIsPresented = false;
        if (CurrentPage is not HomePage)
            await GoToAsync("//HomePage");
        (CurrentPage as HomePage)?.SetHeadingUp(_headingEnabled);
    }

    // Iconos de info (ℹ) del submenu GPS: explican brevemente cada accion.
    private void OnInfoMap(object sender, TappedEventArgs e) =>
        ShowInfo(L("Mapa"), L("Muestra el mapa a pantalla completa sin iniciar ninguna grabación."));
    private void OnInfoPlay(object sender, TappedEventArgs e) =>
        ShowInfo(L("Grabar"), L("Comienza a grabar tu recorrido registrando los puntos GPS."));
    private void OnInfoStop(object sender, TappedEventArgs e) =>
        ShowInfo(L("Parar"), L("Detiene la grabación del recorrido en curso."));
    private void OnInfoSave(object sender, TappedEventArgs e) =>
        ShowInfo(L("Guardar"), L("Guarda el recorrido grabado como una ruta con nombre."));
    private void OnInfoClear(object sender, TappedEventArgs e) =>
        ShowInfo(L("Borrar"), L("Elimina del mapa el recorrido actual sin guardarlo."));
    private void OnInfoFollow(object sender, TappedEventArgs e) =>
        ShowInfo(L("Seguir"), L("Mantiene el mapa centrado automáticamente en tu ubicación en vivo."));
    private void OnInfoHeading(object sender, TappedEventArgs e) =>
        ShowInfo(L("Rumbo"), L("Rota el mapa para que la dirección hacia la que miras apunte hacia arriba."));

    private async void ShowInfo(string title, string message)
    {
        var page = CurrentPage;
        if (page is null)
            return;
        FlyoutIsPresented = false;
        await SocShared.ModernDialog.AlertAsync(page, title, message, L("Entendido"));
    }

    private async void OnRoutesTapped(object sender, TappedEventArgs e) => await NavigateAsync("//RoutesPage");
    private async void OnSettingsTapped(object sender, TappedEventArgs e) => await NavigateAsync("//SettingsPage");
    private async void OnAboutTapped(object sender, TappedEventArgs e) => await NavigateAsync("//AboutPage");

    /// <summary>
    /// Atras (constitucion Mobile 7), por orden: cierra el menu lateral si esta abierto; cierra el
    /// dialogo que haya encima (como tocar fuera de el); en Rutas › ficha de una ruta, vuelve a
    /// Rutas; en Rutas, Configuracion o Acerca de, vuelve al mapa; y en el mapa, la app se oculta
    /// (MoveTaskToBack) sin cerrarse. Nunca para una grabacion: esa vive en el servicio en primer
    /// plano y solo se para con el boton de parar.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        if (FlyoutIsPresented)
        {
            FlyoutIsPresented = false;
            return true;
        }

        if (CurrentPage is ContentPage page && DismissDialog(page))
            return true;

        if (Navigation.NavigationStack.Count > 1)
            return base.OnBackButtonPressed();

        if (CurrentPage is not HomePage)
        {
            Dispatcher.Dispatch(async () => await GoToAsync("//HomePage"));
            return true;
        }

#if ANDROID
        Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.MoveTaskToBack(true);
#endif
        return true;
    }

    /// <summary>
    /// Si la pagina tiene abierto un ModernDialog, lo cierra como si se tocara fuera de el (lo que
    /// equivale a cancelar) y devuelve true. ModernDialog no expone como cerrarlo desde fuera, asi
    /// que se busca su velo y se le manda el toque. Las preguntas que al cerrarse tirarian una ruta
    /// (<see cref="HomePage.IsKeepOrDiscardOpen"/>) no se cierran: atras no hace nada con ellas.
    /// </summary>
    private static bool DismissDialog(ContentPage page)
    {
        if (page.Content is not Grid host)
            return false;

        var overlay = host.Children.OfType<Grid>().LastOrDefault(g => g.StyleId == "__modernDialogOverlay");
        if (overlay is null)
            return false;

        if (page is HomePage { IsKeepOrDiscardOpen: true })
            return true;

        try
        {
            var scrim = overlay.Children.OfType<BoxView>().FirstOrDefault();
            var tap = scrim?.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault();
            var send = typeof(TapGestureRecognizer).GetMethod("SendTapped",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (scrim is not null && tap is not null && send is not null)
            {
                var args = new object?[send.GetParameters().Length];
                args[0] = scrim;
                send.Invoke(tap, args);
            }
        }
        catch
        {
            // Si no se puede cerrar, al menos atras no se lleva la pantalla por delante.
        }
        return true;
    }

    private async Task NavigateAsync(string route)
    {
        // Se navega ANTES de cerrar el menu. Al reves, cerrarlo dispara su animacion y Shell se
        // traga la navegacion: el menu se cerraba y no se iba a ninguna parte, que es por lo que
        // no habia manera de llegar a Rutas (ni, por tanto, al boton de cargar GPX).
        await GoToAsync(route);
        FlyoutIsPresented = false;
    }
}
