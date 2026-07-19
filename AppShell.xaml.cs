using Hiker.Pages;

namespace Hiker;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
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
            await GoToAsync("//HomePage");
        (CurrentPage as HomePage)?.RunMapAction(action);
    }

    private async void OnRoutesTapped(object sender, TappedEventArgs e) => await NavigateAsync("//RoutesPage");
    private async void OnSettingsTapped(object sender, TappedEventArgs e) => await NavigateAsync("//SettingsPage");
    private async void OnAboutTapped(object sender, TappedEventArgs e) => await NavigateAsync("//AboutPage");

    private async Task NavigateAsync(string route)
    {
        FlyoutIsPresented = false;
        await GoToAsync(route);
    }
}
