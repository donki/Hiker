using Hiker.Pages;

namespace Hiker;

public partial class AppShell : Shell
{
    private bool _followEnabled = true;   // el mapa arranca siguiendo al usuario
    private bool _headingEnabled = false; // modo Rumbo desactivado por defecto

    public AppShell()
    {
        InitializeComponent();

        // Pie del menu: version dinamica de la app.
        VersionLabel.Text = $"v{Microsoft.Maui.ApplicationModel.AppInfo.Current.VersionString}";
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
            // "Grabar recorrido" cuando se pulsaba desde Rutas o Configuracion).
            for (int i = 0; i < 20 && CurrentPage is not HomePage; i++)
                await Task.Delay(50);
        }
        (CurrentPage as HomePage)?.RunMapAction(action);
    }

    // Seguir: alterna el recentrado automatico del mapa en la ubicacion en vivo.
    private async void OnActionFollow(object sender, TappedEventArgs e)
    {
        _followEnabled = !_followEnabled;
        FollowLabel.Text = _followEnabled ? "Seguir: Sí" : "Seguir: No";
        FlyoutIsPresented = false;
        if (CurrentPage is not HomePage)
            await GoToAsync("//HomePage");
        (CurrentPage as HomePage)?.SetFollow(_followEnabled);
    }

    // Rumbo: alterna el modo heading-up (rota el mapa segun la brujula).
    private async void OnActionHeading(object sender, TappedEventArgs e)
    {
        _headingEnabled = !_headingEnabled;
        HeadingLabel.Text = _headingEnabled ? "Rumbo: Sí" : "Rumbo: No";
        FlyoutIsPresented = false;
        if (CurrentPage is not HomePage)
            await GoToAsync("//HomePage");
        (CurrentPage as HomePage)?.SetHeadingUp(_headingEnabled);
    }

    // Iconos de info (ℹ) del submenu GPS: explican brevemente cada accion.
    private void OnInfoMap(object sender, TappedEventArgs e) =>
        ShowInfo("Mapa", "Muestra el mapa a pantalla completa sin iniciar ninguna grabación.");
    private void OnInfoPlay(object sender, TappedEventArgs e) =>
        ShowInfo("Iniciar", "Comienza a grabar tu recorrido registrando los puntos GPS.");
    private void OnInfoStop(object sender, TappedEventArgs e) =>
        ShowInfo("Parar", "Detiene la grabación del recorrido en curso.");
    private void OnInfoSave(object sender, TappedEventArgs e) =>
        ShowInfo("Guardar", "Guarda el recorrido grabado como una ruta con nombre.");
    private void OnInfoClear(object sender, TappedEventArgs e) =>
        ShowInfo("Borrar", "Elimina del mapa el recorrido actual sin guardarlo.");
    private void OnInfoFollow(object sender, TappedEventArgs e) =>
        ShowInfo("Seguir", "Mantiene el mapa centrado automáticamente en tu ubicación en vivo.");
    private void OnInfoHeading(object sender, TappedEventArgs e) =>
        ShowInfo("Rumbo", "Rota el mapa para que la dirección hacia la que miras apunte hacia arriba.");

    private async void ShowInfo(string title, string message)
    {
        var page = CurrentPage;
        if (page is null)
            return;
        FlyoutIsPresented = false;
        await SocShared.ModernDialog.AlertAsync(page, title, message, "Entendido");
    }

    private async void OnRoutesTapped(object sender, TappedEventArgs e) => await NavigateAsync("//RoutesPage");
    private async void OnSettingsTapped(object sender, TappedEventArgs e) => await NavigateAsync("//SettingsPage");
    private async void OnAboutTapped(object sender, TappedEventArgs e) => await NavigateAsync("//AboutPage");

    private async Task NavigateAsync(string route)
    {
        // Se navega ANTES de cerrar el menu. Al reves, cerrarlo dispara su animacion y Shell se
        // traga la navegacion: el menu se cerraba y no se iba a ninguna parte, que es por lo que
        // no habia manera de llegar a Rutas (ni, por tanto, al boton de cargar GPX).
        await GoToAsync(route);
        FlyoutIsPresented = false;
    }
}
