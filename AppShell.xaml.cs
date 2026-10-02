using Hiker.Pages;
using Hiker.Presenters;
using Hiker.Services;

namespace Hiker;

public partial class AppShell : Shell
{
    private readonly ShellPresenter _presenter = new(L);

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
        var t = _presenter.Texts();
        (MapLabel.Text, RecordLabel.Text, StopRecordLabel.Text, FollowLabel.Text, HeadingLabel.Text) =
            (t.Map, t.Record, t.Stop, t.Follow, t.Heading);
        (SaveLabel.Text, ClearLabel.Text, RoutesLabel.Text, SettingsLabel.Text, AboutLabel.Text) =
            (t.Save, t.Clear, t.Routes, t.Settings, t.About);
    }

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
        var enabled = _presenter.ToggleFollow();
        FollowLabel.Text = _presenter.FollowText;
        (await ShowHomeAsync())?.SetFollow(enabled);
    }

    // Rumbo: alterna el modo heading-up (rota el mapa segun la brujula).
    private async void OnActionHeading(object sender, TappedEventArgs e)
    {
        var enabled = _presenter.ToggleHeading();
        HeadingLabel.Text = _presenter.HeadingText;
        (await ShowHomeAsync())?.SetHeadingUp(enabled);
    }

    private async Task<HomePage?> ShowHomeAsync()
    {
        FlyoutIsPresented = false;
        if (CurrentPage is not HomePage)
            await GoToAsync("//HomePage");
        return CurrentPage as HomePage;
    }

    // Iconos de info (ℹ) del submenu GPS: explican brevemente cada accion.
    // Iconos (i) del submenu GPS: explican brevemente cada accion.
    private void OnInfoMap(object sender, TappedEventArgs e) => ShowInfo("map");
    private void OnInfoPlay(object sender, TappedEventArgs e) => ShowInfo("play");
    private void OnInfoStop(object sender, TappedEventArgs e) => ShowInfo("stop");
    private void OnInfoSave(object sender, TappedEventArgs e) => ShowInfo("save");
    private void OnInfoClear(object sender, TappedEventArgs e) => ShowInfo("clear");
    private void OnInfoFollow(object sender, TappedEventArgs e) => ShowInfo("follow");
    private void OnInfoHeading(object sender, TappedEventArgs e) => ShowInfo("heading");

    private async void ShowInfo(string action)
    {
        var page = CurrentPage;
        if (page is null)
            return;
        FlyoutIsPresented = false;
        var (title, message) = _presenter.InfoFor(action);
        await SocShared.ModernDialog.AlertAsync(page, title, message, _presenter.Understood);
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
        var overlay = CurrentPage is ContentPage page ? FindDialogOverlay(page) : null;
        switch (ShellPresenter.DecideBack(FlyoutIsPresented, overlay is not null,
                    CurrentPage is HomePage { IsKeepOrDiscardOpen: true },
                    Navigation.NavigationStack.Count, CurrentPage is HomePage))
        {
            case BackAction.CloseFlyout:
                FlyoutIsPresented = false;
                return true;
            case BackAction.DismissDialog:
                DismissDialog(overlay!);
                return true;
            case BackAction.PopPage:
                return base.OnBackButtonPressed();
            case BackAction.GoHome:
                Dispatcher.Dispatch(async () => await GoToAsync("//HomePage"));
                return true;
            case BackAction.MoveToBack:
#if ANDROID
                Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.MoveTaskToBack(true);
#endif
                return true;
            default:
                return true;   // Ignore: la pregunta de guardar/recuperar se queda
        }
    }

    /// <summary>El velo de un ModernDialog abierto en la pagina, si lo hay.</summary>
    private static Grid? FindDialogOverlay(ContentPage page) =>
        (page.Content as Grid)?.Children.OfType<Grid>().LastOrDefault(g => g.StyleId == "__modernDialogOverlay");

    /// <summary>
    /// Si la pagina tiene abierto un ModernDialog, lo cierra como si se tocara fuera de el (lo que
    /// equivale a cancelar) y devuelve true. ModernDialog no expone como cerrarlo desde fuera, asi
    /// que se busca su velo y se le manda el toque. Las preguntas que al cerrarse tirarian una ruta
    /// (<see cref="HomePage.IsKeepOrDiscardOpen"/>) no se cierran: atras no hace nada con ellas.
    /// </summary>
    private static void DismissDialog(Grid overlay)
    {
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
