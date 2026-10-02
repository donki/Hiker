using Hiker.Services;

namespace Hiker.Pages;

// Implementaciones reales (MAUI) de las interfaces que usa la logica. Enlaces finos: lo que hay que
// probar vive en Presenters/ y Services/.

/// <summary>Dialogos con ModernDialog sobre una pagina (nunca AlertDialog nativo).</summary>
public sealed class ModernDialogs(Page page) : IUserDialogs
{
    public Task AlertAsync(string title, string message, string ok) =>
        SocShared.ModernDialog.AlertAsync(page, title, message, ok);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        SocShared.ModernDialog.AlertAsync(page, title, message, accept, cancel);

    public Task<string?> PromptAsync(string title, string message, string accept, string cancel) =>
        SocShared.ModernDialog.PromptAsync(page, title, message, accept, cancel);
}

public sealed class MauiUiThread : IUiThread
{
    public void Post(Func<Task> action) => MainThread.BeginInvokeOnMainThread(async () => await action());
}

/// <summary>Navegacion con Shell; la ficha de una ruta se apila encima de Rutas.</summary>
public sealed class ShellNavigator(Page page, Func<string, Page> routeInfo) : INavigator
{
    public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);
    public Task ShowRouteInfoAsync(string routeName) => page.Navigation.PushAsync(routeInfo(routeName));
}

public sealed class MauiGpxPicker : IGpxPicker
{
    public async Task<(string FileName, string Content)?> PickAsync(string title)
    {
        var result = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                // Android resuelve .gpx como octet-stream/xml (no hay MIME oficial); con "*/*"
                // el selector muestra los .gpx en vez de dejarlos grises. Se valida al parsear.
                { DevicePlatform.Android, new[] { "*/*" } },
                { DevicePlatform.WinUI, new[] { ".gpx", ".xml" } }
            })
        });
        if (result is null)
            return null;

        using var stream = await result.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return (result.FileName, await reader.ReadToEndAsync());
    }
}

/// <summary>Correo con el lanzador del sistema; el resto, en el navegador.</summary>
public sealed class MauiLinkOpener : ILinkOpener
{
    public Task OpenAsync(string uri) => uri.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
        ? Launcher.Default.OpenAsync(uri)
        : Browser.Default.OpenAsync(new Uri(uri), BrowserLaunchMode.SystemPreferred);
}
