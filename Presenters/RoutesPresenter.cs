using System.Collections.ObjectModel;
using System.Globalization;
using Hiker.Helpers;
using Hiker.Models;
using Hiker.Services;

namespace Hiker.Presenters;

/// <summary>Una tarjeta de la lista de rutas, con sus rotulos ya traducidos.</summary>
public class RouteInfo
{
    public string Name { get; set; } = "";
    public double Distance { get; set; }
    public DateTime CreatedDate { get; set; }
    public string DistanceText { get; set; } = "";
    public string DateText { get; set; } = "";
    public RouteData? Route { get; set; }
}

/// <summary>
/// La logica de la pantalla de Rutas (antes en RoutesPage.xaml.cs): listar, abrir en el mapa, ver la
/// ficha, borrar e importar un GPX.
/// </summary>
public sealed class RoutesPresenter
{
    private readonly Func<RouteService?> _routes;
    private readonly Func<string, string> _translate;
    private readonly IUserDialogs _dialogs;
    private readonly INavigator _navigator;
    private readonly IGpxPicker _picker;

    /// <param name="routes">
    /// Se pide en cada uso: en <c>OnAppearing</c> el Handler puede no estar montado aun y el
    /// servicio llegar mas tarde. Antes, con un <c>return</c> mudo, el boton de cargar GPX no hacia
    /// nada y no habia ni error ni aviso que lo delatara.
    /// </param>
    public RoutesPresenter(Func<RouteService?> routes, Func<string, string> translate,
        IUserDialogs dialogs, INavigator navigator, IGpxPicker picker)
    {
        _routes = routes;
        _translate = translate;
        _dialogs = dialogs;
        _navigator = navigator;
        _picker = picker;
    }

    public ObservableCollection<RouteInfo> Routes { get; } = [];

    private string L(string phrase) => _translate(phrase);

    /// <summary>Textos fijos de la pantalla: titulo, cabecera y botones.</summary>
    public (string Title, string Header, string LoadGpx, string Refresh) Texts =>
        (L("Rutas Guardadas"), L("Gestión de Rutas"), L("Cargar GPX"), L("Actualizar"));

    public async Task LoadAsync()
    {
        try
        {
            Routes.Clear();
            if (_routes() is not { } service)
                return;

            foreach (var route in await service.GetAllRoutesAsync())
            {
                var created = service.GetRouteDate(route.routeName);
                // Los rotulos salen traducidos de aqui (en el XAML iban fijos en castellano) y se
                // ponen ANTES de añadir la tarjeta, que RouteInfo no avisa de cambios.
                Routes.Add(new RouteInfo
                {
                    Name = route.routeName,
                    Distance = route.totalDistance, // ProcessData ya calcula la distancia en km
                    CreatedDate = created,
                    Route = route,
                    DistanceText = string.Format(CultureInfo.CurrentCulture, L("Distancia: {0:F2} km"), route.totalDistance),
                    DateText = string.Format(CultureInfo.CurrentCulture, L("Fecha: {0:dd/MM/yyyy}"), created),
                });
            }
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync(L("Error"), string.Format(L("Error cargando las rutas: {0}"), ex.Message), "OK");
        }
    }

    /// <summary>
    /// Abre la ruta en el mapa: se marca y se salta a «//HomePage», que la carga al aparecer (antes
    /// se navegaba a «//GPS», que no es una ruta registrada, y la ruta nunca llegaba a pintarse).
    /// </summary>
    public async Task OpenOnMapAsync(RouteInfo route)
    {
        try
        {
            RouteService.PendingRouteToLoad = route.Name;
            await _navigator.GoToAsync("//HomePage");
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync(L("Error"), string.Format(L("Error cargando la ruta: {0}"), ex.Message), "OK");
        }
    }

    /// <summary>La ficha de la ruta: distancia, desniveles, altitudes, tiempos y perfil.</summary>
    public async Task ShowInfoAsync(RouteInfo route)
    {
        if (_routes() is null)
            return;
        await _navigator.ShowRouteInfoAsync(route.Name);
    }

    public async Task DeleteAsync(RouteInfo route)
    {
        try
        {
            var confirm = await _dialogs.ConfirmAsync(L("Confirmar"),
                string.Format(L("¿Eliminar la ruta «{0}»?"), route.Name), L("Sí"), L("No"));

            if (confirm && _routes() is { } service)
            {
                await service.DeleteRouteAsync(route.Name);
                Routes.Remove(route);
            }
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync(L("Error"), string.Format(L("Error eliminando la ruta: {0}"), ex.Message), "OK");
        }
    }

    /// <summary>
    /// Importar = elegir un GPX, sacar sus puntos y guardarlo como una ruta mas de la app, con el
    /// nombre del fichero. Asi aparece en la lista y se puede abrir en el mapa como el resto.
    /// </summary>
    public async Task ImportGpxAsync()
    {
        try
        {
            if (_routes() is not { } service)
            {
                await _dialogs.AlertAsync(L("Error"),
                    L("No se pudo acceder al servicio de rutas. Cierra y vuelve a abrir la aplicación."), "OK");
                return;
            }

            if (await _picker.PickAsync(L("Seleccionar archivo GPX")) is not { } file)
                return;

            var name = Path.GetFileNameWithoutExtension(file.FileName);
            var points = await Task.Run(() => GpxPointReader.Read(file.Content));
            if (points.Count == 0)
            {
                await _dialogs.AlertAsync(L("Aviso"), L("El archivo GPX no contiene puntos."), "OK");
                return;
            }

            // El servicio guarda el GPX crudo para quien lo pida despues; si su parser (mas
            // estricto) no traga el fichero, la importacion no se pierde por eso.
            try { await service.SetRoute(file.Content); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SetRoute no pudo procesar el GPX: {ex.Message}"); }

            await service.SaveRouteAsync(points, name);
            await LoadAsync();
            await _dialogs.AlertAsync(L("Hecho"), string.Format(L("Ruta «{0}» importada."), name), "OK");
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync(L("Error"), string.Format(L("Error cargando el GPX: {0}"), ex.Message), "OK");
        }
    }
}
