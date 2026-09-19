using Hiker.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Xml.Linq;

namespace Hiker.Pages;

public partial class RoutesPage : ContentPage
{
    private RouteService? _routeService;
    private TranslationService? _translationService;
    public ObservableCollection<RouteInfo> Routes { get; set; } = new();
    
    public ICommand LoadRouteCommand { get; }
    public ICommand DeleteRouteCommand { get; }
    public ICommand InfoRouteCommand { get; }

    public RoutesPage()
    {
        InitializeComponent();

        LoadRouteCommand = new Command<RouteInfo>(OnLoadRoute);
        DeleteRouteCommand = new Command<RouteInfo>(OnDeleteRoute);
        InfoRouteCommand = new Command<RouteInfo>(OnInfoRoute);
        
        routesCollectionView.ItemsSource = Routes;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        ResolveServices();
        TranslateUi();

        await LoadRoutes();
    }

    /// <summary>
    /// Resuelve los servicios. Se cae a <c>IPlatformApplication</c> cuando el Handler todavia no
    /// esta montado: en <c>OnAppearing</c> puede no estarlo, y entonces <c>_routeService</c> se
    /// quedaba a null. Como los manejadores salian con un <c>return</c> mudo, el boton de cargar
    /// GPX no hacia nada y no habia ni error ni aviso que lo delatara.
    /// </summary>
    private void ResolveServices()
    {
        var services = Handler?.MauiContext?.Services ?? IPlatformApplication.Current?.Services;
        if (services is null)
            return;

        _routeService ??= services.GetService<RouteService>();
        _translationService ??= services.GetService<TranslationService>();
    }

    /// <summary>Textos estaticos externalizados (constitucion seccion 8).</summary>
    private void TranslateUi()
    {
        string L(string phrase) => _translationService?.Translate(phrase) ?? phrase;

        Title = L("Rutas Guardadas");
        headerLabel.Text = L("Gestión de Rutas");
        loadGpxButton.Text = L("Cargar GPX");
        refreshButton.Text = L("Actualizar");
    }

    private async Task LoadRoutes()
    {
        try
        {
            Routes.Clear();
            if (_routeService == null) return;
            
            var routes = await _routeService.GetAllRoutesAsync();

            foreach (var route in routes)
            {
                Routes.Add(new RouteInfo
                {
                    Name = route.routeName,
                    Distance = route.totalDistance, // ProcessData ya calcula la distancia en km
                    CreatedDate = _routeService.GetRouteDate(route.routeName),
                    Route = route
                });
            }
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"Error cargando rutas: {ex.Message}", "OK");
        }
    }

    private double CalculateDistance(List<Location> locations)
    {
        if (locations.Count < 2) return 0;
        
        double totalDistance = 0;
        for (int i = 1; i < locations.Count; i++)
        {
            totalDistance += Location.CalculateDistance(
                locations[i-1].Latitude, locations[i-1].Longitude,
                locations[i].Latitude, locations[i].Longitude,
                DistanceUnits.Kilometers);
        }
        
        return totalDistance;
    }

    private async void OnLoadRoute(RouteInfo routeInfo)
    {
        try
        {
            // Se marca la ruta a dibujar y se salta al mapa; HomePage la carga al aparecer.
            // La ruta de Shell es "HomePage" (ver AppShell.xaml): antes se navegaba a "//GPS",
            // que es el Title del FlyoutItem y NO una ruta registrada, asi que la navegacion
            // fallaba y la ruta seleccionada nunca llegaba a pintarse en el mapa.
            RouteService.PendingRouteToLoad = routeInfo.Name;
            await Shell.Current.GoToAsync("//HomePage");
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"Error cargando ruta: {ex.Message}", "OK");
        }
    }

    /// <summary>La ficha de la ruta: distancia, desniveles, altitudes, tiempos y perfil de desnivel.</summary>
    private async void OnInfoRoute(RouteInfo routeInfo)
    {
        if (_routeService is null)
            return;
        await Navigation.PushAsync(new RouteInfoPage(routeInfo.Name, _routeService, _translationService));
    }

    private async void OnDeleteRoute(RouteInfo routeInfo)
    {
        try
        {
            var confirm = await SocShared.ModernDialog.AlertAsync(this, "Confirmar",
                $"¿Eliminar la ruta '{routeInfo.Name}'?", "Sí", "No");

            if (confirm && _routeService != null)
            {
                await _routeService.DeleteRouteAsync(routeInfo.Name);
                Routes.Remove(routeInfo);
            }
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"Error eliminando ruta: {ex.Message}", "OK");
        }
    }

    private async void OnLoadGpxClicked(object sender, EventArgs e)
    {
        try
        {
            ResolveServices();
            if (_routeService is null)
            {
                // Antes esto era un return mudo: el boton parecia roto.
                await SocShared.ModernDialog.AlertAsync(this, "Error",
                    "No se pudo acceder al servicio de rutas. Cierra y vuelve a abrir la aplicación.", "OK");
                return;
            }

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Seleccionar archivo GPX",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    // Android resuelve .gpx como octet-stream/xml (no hay MIME oficial); con "*/*"
                    // el selector muestra los .gpx en vez de dejarlos grises. Se valida al parsear.
                    { DevicePlatform.Android, new[] { "*/*" } },
                    { DevicePlatform.WinUI, new[] { ".gpx", ".xml" } }
                })
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var gpxContent = await reader.ReadToEndAsync();

                // Importar = parsear el GPX y guardarlo como una ruta mas de la app, con el nombre
                // del fichero. Asi aparece en la lista y se puede abrir en el mapa como el resto.
                var name = Path.GetFileNameWithoutExtension(result.FileName);
                var points = await ExtractLocations(gpxContent);
                if (points.Count == 0)
                {
                    await SocShared.ModernDialog.AlertAsync(this, "Aviso", "El archivo GPX no contiene puntos.", "OK");
                    return;
                }

                // El servicio guarda el GPX crudo para quien lo pida despues; si su parser (mas
                // estricto) no traga el fichero, la importacion no se pierde por eso.
                try { await _routeService.SetRoute(gpxContent); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SetRoute no pudo procesar el GPX: {ex.Message}"); }

                await _routeService.SaveRouteAsync(points, name);
                await LoadRoutes();
                await SocShared.ModernDialog.AlertAsync(this, "Éxito", $"Ruta '{name}' importada.", "OK");
            }
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"Error cargando GPX: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Saca los puntos de un GPX sin pasar por el serializador estricto, que solo entendía tracks
    /// (<c>trkpt</c>) del espacio de nombres de GPX 1.1 y obligaba a que cada punto trajera
    /// <c>&lt;time&gt;</c>. Con esas tres condiciones fallaba la mayoría de ficheros descargados:
    /// <list type="bullet">
    ///   <item><description>Un GPX 1.0 usa otro espacio de nombres y no deserializaba nada.</description></item>
    ///   <item><description>Las rutas (<c>rtept</c>) y los puntos sueltos (<c>wpt</c>) se ignoraban:
    ///   salía «el archivo GPX no contiene puntos».</description></item>
    ///   <item><description>Sin <c>&lt;time&gt;</c>, la fecha quedaba en <c>DateTime.MinValue</c> y
    ///   construir el <c>DateTimeOffset</c> reventaba al aplicarle el desfase horario local, con lo
    ///   que se perdía la importación entera.</description></item>
    /// </list>
    /// Aquí se busca por nombre local (cualquier espacio de nombres) y la marca de tiempo es
    /// opcional: para pintar la ruta solo hacen falta latitud y longitud.
    /// </summary>
    private static Task<List<Location>> ExtractLocations(string gpxContent) => Task.Run(() =>
    {
        var points = new List<Location>();
        var doc = XDocument.Parse(gpxContent);

        List<XElement> ByName(string name) =>
            doc.Descendants().Where(e => e.Name.LocalName == name).ToList();

        // Preferencia: track grabado > ruta planificada > puntos sueltos.
        var nodes = ByName("trkpt");
        if (nodes.Count == 0) nodes = ByName("rtept");
        if (nodes.Count == 0) nodes = ByName("wpt");

        foreach (var node in nodes)
        {
            if (!TryParseCoordinate(node.Attribute("lat")?.Value, out var latitude) ||
                !TryParseCoordinate(node.Attribute("lon")?.Value, out var longitude))
                continue;

            var location = new Location(latitude, longitude);

            var child = node.Elements().ToList();

            if (TryParseCoordinate(child.FirstOrDefault(e => e.Name.LocalName == "ele")?.Value, out var elevation))
                location.Altitude = elevation;

            var time = child.FirstOrDefault(e => e.Name.LocalName == "time")?.Value;
            if (DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var timestamp))
                location.Timestamp = timestamp;

            points.Add(location);
        }

        return points;
    });

    private static bool TryParseCoordinate(string? value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadRoutes();
    }
}

public class RouteInfo
{
    public string Name { get; set; } = "";
    public double Distance { get; set; }
    public DateTime CreatedDate { get; set; }
    public dynamic Route { get; set; } = null!;
}