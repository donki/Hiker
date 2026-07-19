using Hiker.Pages;

namespace Hiker;

public partial class AppShell : Shell
{
    // Acciones del mapa (Iniciar/Parar/Guardar/Borrar) que aparecen como submenu indentado bajo
    // la opcion "GPS" del menu, SOLO cuando la pagina actual es el GPS Tracker.
    private readonly List<ShellItem> _gpsActions = new();
    private bool _actionsShown;

    public AppShell()
    {
        InitializeComponent();
        BuildGpsActions();
        Navigated += (_, _) => UpdateGpsActions();
    }

    private void BuildGpsActions()
    {
        _gpsActions.Add(MakeAction("Iniciar", "ic_play.png", "play"));
        _gpsActions.Add(MakeAction("Parar", "ic_stop.png", "stop"));
        _gpsActions.Add(MakeAction("Guardar", "ic_save.png", "save"));
        _gpsActions.Add(MakeAction("Borrar", "ic_trash.png", "clear"));
    }

    private ShellItem MakeAction(string text, string icon, string action)
    {
        var item = new MenuItem { Text = text, IconImageSource = icon };
        item.Clicked += (_, _) =>
        {
            FlyoutIsPresented = false;
            if (CurrentPage is HomePage home)
                home.RunMapAction(action);
        };
        return item;   // conversion implicita MenuItem -> MenuShellItem
    }

    private void UpdateGpsActions()
    {
        var onGps = CurrentPage is HomePage;
        if (onGps && !_actionsShown)
        {
            // Se insertan justo despues de la opcion "GPS" (indice 0) para que se lean como submenu.
            for (int i = 0; i < _gpsActions.Count; i++)
                Items.Insert(1 + i, _gpsActions[i]);
            _actionsShown = true;
        }
        else if (!onGps && _actionsShown)
        {
            foreach (var it in _gpsActions)
                Items.Remove(it);
            _actionsShown = false;
        }
    }
}
